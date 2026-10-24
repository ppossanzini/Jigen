using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Opens a persisted collection: every segment listed by the manifest plus the
/// tombstone list. Owns the stored lists it opens, so disposing it releases the
/// files; <see cref="Index"/> stays valid until then.
/// </summary>
public sealed class SparseCollectionStorage : IAsyncDisposable
{
    private readonly List<StoredList<SparseTermDirectoryEntry, ValueTuple>> _directories = [];
    private readonly List<StoredList<SparsePostingBlock, ValueTuple>> _blocks = [];
    private readonly StoredList<SparseTombstone, ValueTuple> _tombstones;

    private SparseCollectionStorage(SparseCollectionIndex index,
        StoredList<SparseTombstone, ValueTuple> tombstones)
    {
        Index = index;
        _tombstones = tombstones;
    }

    /// <summary>
    /// Opens the collection using tombstones supplied by the caller, typically
    /// the live list of the writer in this process: a second handle on the same
    /// file would not see records that are not flushed yet.
    /// </summary>
    public static SparseCollectionStorage Open(string collectionDirectoryPath,
        IReadOnlyList<SparseTombstone> tombstones)
    {
        ArgumentNullException.ThrowIfNull(tombstones);
        return Open(collectionDirectoryPath, tombstones, openTombstoneFile: false);
    }

    /// <summary>Search over the whole collection.</summary>
    public SparseCollectionIndex Index { get; }

    /// <summary>
    /// Opens every segment of the collection. The manifest is the only directory
    /// listing needed: a segment directory that it does not mention is ignored.
    /// </summary>
    public static SparseCollectionStorage Open(string collectionDirectoryPath) =>
        Open(collectionDirectoryPath, null, openTombstoneFile: true);

    private static SparseCollectionStorage Open(string collectionDirectoryPath,
        IReadOnlyList<SparseTombstone> suppliedTombstones, bool openTombstoneFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionDirectoryPath);
        var segments = new List<SparseSegment>();
        var directories = new List<StoredList<SparseTermDirectoryEntry, ValueTuple>>();
        var blocks = new List<StoredList<SparsePostingBlock, ValueTuple>>();
        var tombstones = openTombstoneFile
            ? new StoredList<SparseTombstone, ValueTuple>(
                new StoreListOptions { FilePath = Path.Combine(collectionDirectoryPath, "tombstones") }, default)
            : null!;

        try
        {
            foreach (var generation in SparseCollectionManifest.Read(collectionDirectoryPath))
            {
                var segmentPath = SparseCollectionWriter.SegmentPath(
                    Path.Combine(collectionDirectoryPath, "segments"), generation);
                var directory = new StoredList<SparseTermDirectoryEntry, ValueTuple>(
                    new StoreListOptions { FilePath = Path.Combine(segmentPath, "terms") }, default);
                directories.Add(directory);
                var blockList = new StoredList<SparsePostingBlock, ValueTuple>(
                    new StoreListOptions { FilePath = Path.Combine(segmentPath, "blocks") }, default);
                blocks.Add(blockList);
                segments.Add(new SparseSegment(generation, new SparseIndex(directory, blockList)));
            }

            var retired = suppliedTombstones is not null
                ? [.. suppliedTombstones]
                : ReadTombstones(tombstones);

            var storage = new SparseCollectionStorage(
                new SparseCollectionIndex(segments, retired), tombstones);
            storage._directories.AddRange(directories);
            storage._blocks.AddRange(blocks);
            return storage;
        }
        catch
        {
            foreach (var list in directories)
                list.DisposeAsync().AsTask().GetAwaiter().GetResult();
            foreach (var list in blocks)
                list.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (tombstones is not null)
                tombstones.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    private static List<SparseTombstone> ReadTombstones(StoredList<SparseTombstone, ValueTuple> tombstones)
    {
        var retired = new List<SparseTombstone>(tombstones.Count);
        for (var position = 0; position < tombstones.Count; position++)
            retired.Add(tombstones[position]);
        return retired;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var list in _directories)
            await list.DisposeAsync();
        foreach (var list in _blocks)
            await list.DisposeAsync();
        if (_tombstones is not null)
            await _tombstones.DisposeAsync();
    }
}
