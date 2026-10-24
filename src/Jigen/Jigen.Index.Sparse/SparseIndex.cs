using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Read-only sparse search over one collection. The caller owns both stored lists
/// and keeps their contents and positions unchanged for this index's lifetime.
/// </summary>
public sealed class SparseIndex
{
    private readonly StoredList<SparseTermDirectoryEntry, ValueTuple> _directory;
    private readonly StoredList<SparsePostingBlock, ValueTuple> _blocks;
    private readonly Dictionary<int, int> _termPositions = [];

    public SparseIndex(StoredList<SparseTermDirectoryEntry, ValueTuple> directory,
        StoredList<SparsePostingBlock, ValueTuple> blocks)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(blocks);
        _directory = directory;
        _blocks = blocks;

        for (var position = 0; position < directory.Count; position++)
        {
            var entry = directory[position];
            if (!_termPositions.TryAdd(entry.TermId, position))
                throw new InvalidDataException($"Duplicate sparse term identifier: {entry.TermId}.");
        }
    }

    public int TermCount => _termPositions.Count;

    public bool TryGetTerm(int termId, out SparseTermDirectoryEntry entry)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(termId);
        entry = _termPositions.TryGetValue(termId, out var position) ? _directory[position] : null;
        return entry is not null;
    }

    /// <summary>
    /// Returns at most k distinct documents, ordered by descending score then binary key.
    /// Scores are maximum inner products over vectors sharing a query term.
    /// Products and sums use double precision, with terms accumulated by ascending identifier.
    /// Zero-weight query terms still participate in matching.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> Search(IReadOnlyDictionary<int, float> query, int k,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        cancellationToken.ThrowIfCancellationRequested();

        var terms = new List<(SparseTermDirectoryEntry Directory, float Weight)>();
        foreach (var (termId, weight) in query.OrderBy(term => term.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentOutOfRangeException.ThrowIfNegative(termId);
            if (!float.IsFinite(weight))
                throw new ArgumentOutOfRangeException(nameof(query), "Query weights must be finite.");
            if (TryGetTerm(termId, out var entry) && entry.BlockReferences.Length > 0)
                terms.Add((entry, weight));
        }

        if (k == 0 || terms.Count == 0)
            return Array.Empty<SparseSearchResult>();

        return SparseMaxScoreSearch.Execute(terms, _blocks, k, cancellationToken);
    }
}
