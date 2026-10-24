using Jigen.DataStructures;
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
        IReadOnlyDictionary<int, float> query, int k, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(k);

        var storage = OpenSparseReader(collection);
        return storage is null
            ? Array.Empty<SparseSearchResult>()
            : storage.Index.Search(query, k, cancellationToken);
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
