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
    private readonly Dictionary<string, SparseReaderLease> _sparseReaders = new(StringComparer.Ordinal);
    // Generation and tombstone count the cached reader was built from: a delete
    // adds a tombstone without a new segment and must still invalidate it.
    private readonly Dictionary<string, (long Generation, int Tombstones)> _sparseReaderStates = new(StringComparer.Ordinal);
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

    private SparseOptions SparsePolicy => Options.Sparse ?? new SparseOptions();

    /// <summary>
    /// Appends the sparse vectors of a persisted entry. Called by the writer
    /// thread, so the sparse files follow the same single-writer rule as the
    /// dense ones. A collection is committed as a new segment only once its
    /// buffer reaches <see cref="SparseOptions.CommitMinTerms"/> or
    /// <see cref="SparseOptions.CommitMaxDelay"/> has passed since its last commit.
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

            var policy = SparsePolicy;
            foreach (var collection in touched)
            {
                var writer = _sparseWriters[collection];
                if (writer.BufferedTerms >= policy.CommitMinTerms || writer.SinceLastCommit >= policy.CommitMaxDelay)
                    writer.Commit();
            }
        }
    }

    /// <summary>
    /// Writes every buffered sparse document as a segment, so it becomes
    /// searchable now instead of at the next threshold.
    /// </summary>
    public void FlushSparseIndexes() => CommitSparseBuffers(force: true);

    /// <summary>
    /// Commits the buffers: all of them when <paramref name="force"/> is true,
    /// otherwise only those past <see cref="SparseOptions.CommitMaxDelay"/>.
    /// </summary>
    internal void CommitSparseBuffers(bool force)
    {
        lock (_sparseLock)
        {
            var maxDelay = SparsePolicy.CommitMaxDelay;
            foreach (var writer in _sparseWriters.Values)
                if (writer.BufferedDocuments > 0 && (force || writer.SinceLastCommit >= maxDelay))
                    writer.Commit();
        }
    }

    /// <summary>
    /// One pass of the background sparse worker: commits the expired buffers,
    /// then merges the segments that <see cref="SparseMergePolicy"/> selects by
    /// size, until no merge is due.
    /// </summary>
    internal void RunSparseMaintenance()
    {
        if (!WritesSparseSection)
            return;

        CommitSparseBuffers(force: false);

        var policy = SparsePolicy;
        if (policy.MaxSegmentBytes <= 0)
            return;

        lock (_sparseLock)
        {
            if (!Directory.Exists(SparsePath))
                return;

            foreach (var directory in Directory.EnumerateDirectories(SparsePath))
            {
                // Every merge removes at least one segment, so the loop ends.
                while (true)
                {
                    var sizes = SparseCollectionManifest.Read(directory)
                        .Select(generation => (generation, SparseCollectionCompactor.SegmentSizeBytes(directory, generation)))
                        .ToArray();
                    var selected = SparseMergePolicy.Select(sizes, policy.MinSegmentBytes, policy.MaxSegmentBytes);
                    if (selected.Count < 2)
                        break;
                    CompactSparseCollection(directory, selected);
                }
            }
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
    /// Metrics of a collection's sparse index, or null when the collection has
    /// none. Reads the segments the searches read, so the numbers describe the
    /// same state; the postings count is not included because summing it means
    /// walking every posting block.
    /// </summary>
    public SparseCollectionInfo GetSparseInfo(string collection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);

        var path = SparseCollectionPath(collection);
        if (!Directory.Exists(path))
            return null!;

        var info = new SparseCollectionInfo
        {
            SizeBytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length)
        };

        var lease = AcquireSparseReader(collection);
        if (lease is null)
            return info;

        try
        {
            var storage = lease.Storage;
            foreach (var segment in storage.Index.Segments)
            {
                info.Terms += segment.Index.TermCount;
                info.Blocks += segment.Index.BlockCount;
            }

            info.Segments = storage.Index.Segments.Count;
            info.Tombstones = storage.Index.TombstoneCount;
            return info;
        }
        finally
        {
            lease.Release();
        }
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

        var lease = AcquireSparseReader(collection);
        if (lease is null)
            return Array.Empty<SparseSearchResult>();

        try
        {
            if (filter is null)
                return lease.Storage.Index.Search(query, k, null, minimumScore, cancellationToken);

            return lease.Storage.Index.Search(query, k,
                documentId => MatchesFilter(collection, documentId, filter), minimumScore, cancellationToken);
        }
        finally
        {
            lease.Release();
        }
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

    /// <summary>
    /// Returns an acquired lease on the current reader of the collection, or
    /// null when it has no sparse index. The caller must release it.
    /// </summary>
    private SparseReaderLease AcquireSparseReader(string collection)
    {
        lock (_sparseLock)
        {
            var path = SparseCollectionPath(collection);
            if (!Directory.Exists(path))
                return null!;

            // A reader built before the last commit or delete would silently
            // serve stale results.
            var liveWriter = _sparseWriters.GetValueOrDefault(collection);
            var state = liveWriter is not null
                ? (liveWriter.LastGeneration, liveWriter.Tombstones.Count)
                : (ReadLastGeneration(path), -1);
            if (_sparseReaders.TryGetValue(collection, out var cached) &&
                _sparseReaderStates.TryGetValue(collection, out var cachedState) &&
                cachedState == state)
                return cached.Acquire();

            if (cached is not null)
                RetireSparseReader(collection);

            var reopened = new SparseReaderLease(liveWriter is not null
                ? SparseCollectionStorage.Open(path, liveWriter.Tombstones)
                : SparseCollectionStorage.Open(path));
            _sparseReaders[collection] = reopened;
            _sparseReaderStates[collection] = state;
            return reopened.Acquire();
        }
    }

    // Called under _sparseLock: no new lease can start, and the searches still
    // running on the old files finish before they are released.
    private void RetireSparseReader(string collection)
    {
        if (_sparseReaders.Remove(collection, out var reader))
            reader.DisposeWhenIdle();
        _sparseReaderStates.Remove(collection);
    }

    private static long ReadLastGeneration(string path)
    {
        long last = 0;
        foreach (var generation in SparseCollectionManifest.Read(path))
            if (generation > last)
                last = generation;
        return last;
    }

    /// <summary>
    /// Commits the buffered documents, then closes every sparse writer and
    /// reader; called by <see cref="Close"/>.
    /// </summary>
    private void CloseSparseIndexes()
    {
        lock (_sparseLock)
        {
            foreach (var writer in _sparseWriters.Values)
            {
                writer.Commit();
                writer.Dispose();
            }
            _sparseWriters.Clear();

            foreach (var collection in _sparseReaders.Keys.ToArray())
                RetireSparseReader(collection);
        }
    }

    /// <summary>Merges the segments of every sparse collection that has more than one.</summary>
    private void CompactSparseIndexes()
    {
        lock (_sparseLock)
        {
            // Committed first: a buffer would otherwise land in a segment right
            // after the shrink.
            foreach (var writer in _sparseWriters.Values)
                writer.Commit();

            if (!Directory.Exists(SparsePath))
                return;

            foreach (var directory in Directory.EnumerateDirectories(SparsePath))
                if (NeedsSparseCompaction(directory))
                    CompactSparseCollection(directory);
        }
    }

    /// <summary>
    /// Merges one collection: every segment, or only <paramref name="generations"/>.
    /// Called under <see cref="_sparseLock"/>: the buffer is committed first so
    /// nothing is lost, and the writer and reader are closed because the
    /// compaction rewrites the manifest and may clear the tombstones they hold
    /// in memory.
    /// </summary>
    private void CompactSparseCollection(string directory, IReadOnlyList<long> generations = null)
    {
        foreach (var collection in _sparseWriters.Keys.ToArray())
        {
            if (!string.Equals(SparseCollectionPath(collection), directory, StringComparison.Ordinal))
                continue;
            var writer = _sparseWriters[collection];
            writer.Commit();
            writer.Dispose();
            _sparseWriters.Remove(collection);
        }

        foreach (var collection in _sparseReaders.Keys.ToArray())
            if (string.Equals(SparseCollectionPath(collection), directory, StringComparison.Ordinal))
                RetireSparseReader(collection);

        var compactor = new SparseCollectionCompactor(directory);
        if (generations is null)
            compactor.Compact();
        else
            compactor.CompactSegments(generations);
    }

    /// <summary>
    /// Reader shared by concurrent searches. The cache holds one reference;
    /// the files are released only once the cache dropped it and every search
    /// using it has finished.
    /// </summary>
    private sealed class SparseReaderLease(SparseCollectionStorage storage)
    {
        private int _users;

        public SparseCollectionStorage Storage { get; } = storage;

        public SparseReaderLease Acquire()
        {
            Interlocked.Increment(ref _users);
            return this;
        }

        public void Release() => Interlocked.Decrement(ref _users);

        public void DisposeWhenIdle()
        {
            var wait = new SpinWait();
            while (Volatile.Read(ref _users) > 0)
                wait.SpinOnce();
            Storage.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// A collection is worth compacting when it reads more than one segment, or
    /// when it carries tombstones: retired copies of a single-segment collection
    /// would otherwise stay on disk forever, because nothing else ever rewrites
    /// that segment.
    /// </summary>
    private static bool NeedsSparseCompaction(string directory)
    {
        if (SparseCollectionManifest.Read(directory).Count > 1)
            return true;

        var storage = SparseCollectionStorage.Open(directory);
        try
        {
            return storage.Index.TombstoneCount > 0;
        }
        finally
        {
            storage.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
