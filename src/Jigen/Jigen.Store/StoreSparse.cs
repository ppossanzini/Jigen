using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;
using Jigen.Index.Sparse;

namespace Jigen;

/// <summary>
/// Sparse index of the collections of this store. One collection owns one
/// directory under <c>{database}.sparse/</c>; documents are appended from the
/// writer thread and become searchable at each batch commit.
/// </summary>
public partial class Store
{
    private readonly Dictionary<string, SparseCollectionWriter> _sparseWriters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SparseCollectionStorage> _sparseReaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _sparseReaderGenerations = new(StringComparer.Ordinal);
    private readonly Lock _sparseLock = new();
    // Keys replayed from the WAL: the segment on disk may already hold them, so
    // the copy is retired before indexing the replayed version.
    private readonly HashSet<string> _replayedSparseKeys = new(StringComparer.Ordinal);
    // Keys whose last WAL record is a deletion: replay must not index them
    // again, or the document comes back after every crash.
    private readonly HashSet<string> _replayDeletedKeys = new(StringComparer.Ordinal);

    /// <summary>Directory holding one sparse index per collection.</summary>
    public string SparsePath => Path.Combine(Options.DataBasePath, $"{Options.DataBaseName}.sparse");

    internal string SparseCollectionPath(string collection) => Path.Combine(SparsePath, EncodeName(collection));

    // Collection names can contain characters a file system rejects; the
    // encoding stays reversible and readable for the common case.
    private static string EncodeName(string collection)
    {
        var builder = new System.Text.StringBuilder(collection.Length);
        foreach (var character in collection)
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_' + ((int)character).ToString("x4"));
        return builder.ToString();
    }

    /// <summary>
    /// Appends the sparse vectors of a persisted entry and publishes the batch.
    /// Called by the writer thread, so the sparse files follow the same
    /// single-writer rule as the dense ones.
    /// </summary>
    internal void IndexSparseEntries(IReadOnlyList<VectorEntry> entries)
    {
        if (!WritesSparseSection)
            return;

        lock (_sparseLock)
        {
            HashSet<string> touched = null!;
            HashSet<string> written = null!;
            foreach (var entry in entries)
            {
                if (entry.SparseVectors is not { Count: > 0 })
                    continue;

                var writer = GetSparseWriter(entry.CollectionName);
                // An overwrite replaces the dense record but the sparse index is
                // append-only: the previous copies must be retired first, or the
                // document would be served twice with stale weights.
                var alreadyIndexed = (written ??= new HashSet<string>(StringComparer.Ordinal))
                        .Add(entry.CollectionName + "\u0000" + Convert.ToHexString(entry.Id)) == false;
                var key = ReplayKey(entry.CollectionName, entry.Id);
                var replayed = _replayedSparseKeys.Remove(key);
                var replayedDelete = _replayDeletedKeys.Remove(key);
                if (alreadyIndexed || replayed || replayedDelete || ExistedBefore(entry.CollectionName, entry.Id))
                    writer.Delete(entry.Id);
                if (replayedDelete)
                    continue;

                writer.Add(entry.Id, entry.SparseVectors);
                (touched ??= new HashSet<string>(StringComparer.Ordinal)).Add(entry.CollectionName);
            }

            if (touched is null)
                return;

            foreach (var collection in touched)
                _sparseWriters[collection].Commit();
        }
    }

    internal void DeleteSparseEntry(string collection, byte[] id)
    {
        if (!WritesSparseSection)
            return;

        lock (_sparseLock)
        {
            if (!_sparseWriters.ContainsKey(collection) && !Directory.Exists(SparseCollectionPath(collection)))
                return;

            GetSparseWriter(collection).Delete(id);
        }
    }

    /// <summary>
    /// Records that the key comes from WAL replay: the segment already on disk
    /// may hold it even when the position index does not, so the previous copy
    /// must be retired before indexing the replayed one.
    /// </summary>
    internal void MarkReplayedSparseEntry(VectorEntry entry)
    {
        if (entry.SparseVectors is not { Count: > 0 } || !WritesSparseSection)
            return;
        lock (_sparseLock)
        {
            var key = ReplayKey(entry.CollectionName, entry.Id);
            _replayDeletedKeys.Remove(key);
            _replayedSparseKeys.Add(key);
        }
    }

    /// <summary>
    /// Records that a replayed WAL record deletes the key: the sparse copy must
    /// stay retired even though earlier insert records are replayed too.
    /// </summary>
    internal void MarkReplayedSparseDelete(string collection, byte[] id)
    {
        if (!WritesSparseSection)
            return;
        lock (_sparseLock)
        {
            var key = ReplayKey(collection, id);
            _replayedSparseKeys.Remove(key);
            _replayDeletedKeys.Add(key);
        }
    }

    private static string ReplayKey(string collection, byte[] id) =>
        collection + "\u0000" + Convert.ToHexString(id);

    // True when the collection already held this key before the batch was
    // registered: the dense position index is updated after the sparse step.
    private bool ExistedBefore(string collection, byte[] id) =>
        PositionIndex.TryGetValue(collection, out var index) && index.ContainsKey(id);

    private SparseCollectionWriter GetSparseWriter(string collection)
    {
        if (_sparseWriters.TryGetValue(collection, out var writer))
            return writer;

        writer = new SparseCollectionWriter(SparseCollectionPath(collection));
        _sparseWriters[collection] = writer;
        return writer;
    }

    /// <summary>
    /// Best k documents of a collection by sparse score. Returns an empty list
    /// when the collection has no sparse index yet.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> SearchSparse(string collection,
        IReadOnlyDictionary<int, float> query, int k, CancellationToken cancellationToken = default) =>
        SearchSparse(collection, query, k, null, cancellationToken);

    /// <summary>
    /// Same search, keeping only the documents that satisfy the filter. The
    /// filter reads the stored content of each candidate and is evaluated on the
    /// serialized bytes, so a rejected document never occupies a slot of the
    /// top-k; the cost is one content read per candidate examined.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> SearchSparse(string collection,
        IReadOnlyDictionary<int, float> query, int k, IFilterExpression filter,
        CancellationToken cancellationToken = default) =>
        SearchSparse(collection, query, k, filter, double.NegativeInfinity, cancellationToken);

    /// <summary>
    /// Same search, keeping only the documents whose sparse score reaches
    /// <paramref name="minimumScore"/>. The value also prunes earlier, so a
    /// large k with a high minimum costs less than a plain large k.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> SearchSparse(string collection,
        IReadOnlyDictionary<int, float> query, int k, IFilterExpression filter, double minimumScore,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(k);

        var storage = OpenSparseReader(collection);
        if (storage is null)
            return Array.Empty<SparseSearchResult>();
        if (filter is null)
            return storage.Index.Search(query, k, null, minimumScore, cancellationToken);

        return storage.Index.Search(query, k,
            documentId => MatchesFilter(collection, documentId, filter), minimumScore, cancellationToken);
    }


    /// <summary>
    /// Hybrid search over the two halves of a collection. Either query may be
    /// omitted; supplying neither is an error.
    /// <para>
    /// The scores live on different scales, so they are fused by rank: a document
    /// scores 1 / (60 + position) in each list it appears in and the values are
    /// summed. <paramref name="minDenseScore"/> and <paramref name="minSparseScore"/>
    /// drop the weak hits of each half before the fusion.
    /// </para>
    /// </summary>
    public IReadOnlyList<SparseHybridHit> SearchHybrid(string collection, float[] embeddings,
        IReadOnlyDictionary<int, float> sparseQuery, int top = 10, int efSearch = 0,
        IFilterExpression filter = null, double minDenseScore = double.NegativeInfinity,
        double minSparseScore = double.NegativeInfinity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        if (embeddings is not { Length: > 0 } && sparseQuery is null)
            throw new ArgumentException("A hybrid search needs a dense query, a sparse query, or both.");
        if (top <= 0)
            return Array.Empty<SparseHybridHit>();

        var fused = new Dictionary<string, SparseHybridHit>(StringComparer.Ordinal);

        void Add(byte[] key, VectorEntry entry, double contribution, float? dense, float? sparse)
        {
            var identity = Convert.ToHexString(key);
            if (fused.TryGetValue(identity, out var existing))
                fused[identity] = existing with
                {
                    Entry = existing.Entry ?? entry,
                    Score = existing.Score + contribution,
                    DenseScore = dense ?? existing.DenseScore,
                    SparseScore = sparse ?? existing.SparseScore
                };
            else
                fused[identity] = new SparseHybridHit(key, entry, contribution, dense, sparse);
        }

        if (embeddings is { Length: > 0 })
        {
            var rank = 0;
            foreach (var hit in Jigen.Extensions.SearchExtensions.SearchDense(this, collection, embeddings,
                top, efSearch, filter))
            {
                if (hit.score < minDenseScore)
                    continue;
                Add(hit.entry.Id, hit.entry, 1d / (ReciprocalRankOffset + ++rank), hit.score, null);
            }
        }

        if (sparseQuery is not null)
        {
            var sparseHits = SearchSparse(collection, sparseQuery, top, filter, minSparseScore, cancellationToken);
            for (var rank = 0; rank < sparseHits.Count; rank++)
            {
                var key = sparseHits[rank].DocumentId.ToArray();
                var entry = fused.TryGetValue(Convert.ToHexString(key), out var known)
                    ? known.Entry
                    : PositionIndex.TryGetValue(collection, out var index) && index.ContainsKey(key)
                        ? new VectorEntry
                        {
                            Id = key,
                            CollectionName = collection,
                            Content = GetContent(collection, key) ?? ReadOnlyMemory<byte>.Empty
                        }
                        : null!;
                Add(key, entry, 1d / (ReciprocalRankOffset + rank + 1), null, (float)sparseHits[rank].Score);
            }
        }

        return fused.Values
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Key, ByteArrayOrder.Instance)
            .Take(top)
            .ToArray();
    }

    // Standard reciprocal-rank constant: damps the influence of the first positions
    // without requiring a normalization of the two scores.
    private const int ReciprocalRankOffset = 60;

    private bool MatchesFilter(string collection, ReadOnlySpan<byte> documentId, IFilterExpression filter)
    {
        var content = GetContent(collection, documentId.ToArray());
        return content is { Length: > 0 } &&
               MessagePackSerializedDocumentFilter.Instance.MatchesFilter(content, filter);
    }

    private SparseCollectionStorage OpenSparseReader(string collection)
    {
        lock (_sparseLock)
        {
            var path = SparseCollectionPath(collection);
            if (!Directory.Exists(path))
                return null!;

            // The writer publishes a new segment per commit; a reader built
            // before that commit would silently serve stale results.
            var generation = _sparseWriters.TryGetValue(collection, out var writer) ? writer.LastGeneration : ReadLastGeneration(path);
            if (_sparseReaders.TryGetValue(collection, out var cached) &&
                _sparseReaderGenerations.TryGetValue(collection, out var cachedGeneration) &&
                cachedGeneration == generation)
                return cached;

            if (cached is not null)
                cached.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var reopened = _sparseWriters.TryGetValue(collection, out var liveWriter)
                ? SparseCollectionStorage.Open(path, liveWriter.Tombstones)
                : SparseCollectionStorage.Open(path);
            _sparseReaders[collection] = reopened;
            _sparseReaderGenerations[collection] = generation;
            return reopened;
        }
    }

    private static long ReadLastGeneration(string path)
    {
        long last = 0;
        foreach (var generation in SparseCollectionManifest.Read(path))
            if (generation > last)
                last = generation;
        return last;
    }

    /// <summary>Closes every sparse writer and reader; called by <see cref="Close"/>.</summary>
    private void CloseSparseIndexes()
    {
        lock (_sparseLock)
        {
            foreach (var writer in _sparseWriters.Values)
                writer.Dispose();
            _sparseWriters.Clear();

            foreach (var reader in _sparseReaders.Values)
                reader.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _sparseReaders.Clear();
            _sparseReaderGenerations.Clear();
        }
    }

    /// <summary>Merges the segments of every sparse collection that has more than one.</summary>
    private void CompactSparseIndexes()
    {
        lock (_sparseLock)
        {
            _sparseWriters.Clear();
            foreach (var reader in _sparseReaders.Values)
                reader.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _sparseReaders.Clear();
            _sparseReaderGenerations.Clear();

            if (!Directory.Exists(SparsePath))
                return;

            foreach (var directory in Directory.EnumerateDirectories(SparsePath))
                if (SparseCollectionManifest.Read(directory).Count > 1)
                    new SparseCollectionCompactor(directory).Compact();
        }
    }
}
