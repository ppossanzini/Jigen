using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Collects the sparse vectors of a collection and writes its term directory and
/// posting blocks.
/// <para>
/// Postings of a term must be ordered by document key and vector index, so the
/// index is written in one pass after every document is known: it cannot be
/// appended in arrival order. A rebuild after an update or a deletion therefore
/// produces a new index rather than editing the existing one.
/// </para>
/// </summary>
public sealed class SparseIndexBuilder
{
    private readonly Dictionary<int, List<(byte[] DocumentId, int VectorIndex, float Weight)>> _postings = [];

    /// <summary>Maximum number of postings in a block.</summary>
    public int BlockSize { get; init; } = 64;

    /// <summary>Adds one document; a document without sparse vectors contributes nothing.</summary>
    public void Add(byte[] documentId, IReadOnlyList<SparseTerm[]> vectors)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(vectors);
        if (documentId.Length == 0)
            throw new ArgumentException("A document requires a key.", nameof(documentId));
        if (BlockSize <= 0)
            throw new InvalidOperationException("Block size must be positive.");

        for (var vectorIndex = 0; vectorIndex < vectors.Count; vectorIndex++)
        {
            var vector = vectors[vectorIndex] ?? throw new ArgumentException(
                "A sparse vector cannot be null.", nameof(vectors));

            // The caller does not have to order the pairs: the stored order is
            // this builder's business. Only a repeated term is an error, because
            // two weights for one term have no defined meaning.
            var ordered = new SparseTerm[vector.Length];
            vector.CopyTo(ordered, 0);
            Array.Sort(ordered, (left, right) => left.Term.CompareTo(right.Term));

            for (var position = 0; position < ordered.Length; position++)
            {
                var term = ordered[position];
                if (term.Term < 0)
                    throw new ArgumentOutOfRangeException(nameof(vectors), "Term identifiers must be non-negative.");
                if (!float.IsFinite(term.Weight))
                    throw new ArgumentOutOfRangeException(nameof(vectors), "Weights must be finite.");
                if (position > 0 && ordered[position - 1].Term == term.Term)
                    throw new ArgumentException(
                        $"Term {term.Term} appears twice in the same sparse vector.", nameof(vectors));

                if (!_postings.TryGetValue(term.Term, out var list))
                    _postings[term.Term] = list = [];
                list.Add((documentId, vectorIndex, term.Weight));
            }
        }
    }

    /// <summary>
    /// Writes the index of a collection into <paramref name="directoryPath"/>,
    /// which must not already hold one. The caller builds into a fresh directory
    /// and swaps it in, so a failed build never damages the live index.
    /// </summary>
    public SparseIndexBuildSummary Write(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
        if (File.Exists(Path.Combine(directoryPath, "terms")) ||
            File.Exists(Path.Combine(directoryPath, "blocks")))
            throw new IOException($"A sparse index already exists at '{directoryPath}'.");

        var termOptions = new StoreListOptions { FilePath = Path.Combine(directoryPath, "terms") };
        var blockOptions = new StoreListOptions { FilePath = Path.Combine(directoryPath, "blocks") };
        var terms = new StoredList<SparseTermDirectoryEntry, ValueTuple>(termOptions, default);
        var blocks = new StoredList<SparsePostingBlock, ValueTuple>(blockOptions, default);
        try
        {
            var postingCount = WriteTerms(terms, blocks);
            terms.Flush();
            blocks.Flush();
            return new SparseIndexBuildSummary(_postings.Count, postingCount, blocks.Count);
        }
        finally
        {
            blocks.DisposeAsync().AsTask().GetAwaiter().GetResult();
            terms.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private int WriteTerms(StoredList<SparseTermDirectoryEntry, ValueTuple> terms,
        StoredList<SparsePostingBlock, ValueTuple> blocks)
    {
        var postingCount = 0;
        foreach (var termId in _postings.Keys.Order())
        {
            var postings = _postings[termId];
            postings.Sort(ComparePosting);
            for (var index = 1; index < postings.Count; index++)
                if (ComparePosting(postings[index - 1], postings[index]) == 0)
                    throw new InvalidDataException($"The same document and vector repeat term {termId}.");

            var references = new List<SparseBlockReference>((postings.Count + BlockSize - 1) / BlockSize);
            for (var start = 0; start < postings.Count; start += BlockSize)
            {
                var length = Math.Min(BlockSize, postings.Count - start);
                var items = new SparsePosting[length];
                for (var position = 0; position < length; position++)
                {
                    var posting = postings[start + position];
                    items[position] = new SparsePosting(posting.DocumentId, posting.VectorIndex, posting.Weight);
                }

                var block = new SparsePostingBlock(items);
                references.Add(new SparseBlockReference(blocks.Count, block));
                blocks.Add(block);
            }

            terms.Add(new SparseTermDirectoryEntry(termId, references));
            postingCount += postings.Count;
        }

        return postingCount;
    }

    private static int ComparePosting((byte[] DocumentId, int VectorIndex, float Weight) left,
        (byte[] DocumentId, int VectorIndex, float Weight) right)
    {
        var comparison = left.DocumentId.AsSpan().SequenceCompareTo(right.DocumentId);
        return comparison != 0 ? comparison : left.VectorIndex.CompareTo(right.VectorIndex);
    }
}
