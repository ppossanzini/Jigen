using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Merges every live segment of a collection into one new segment, dropping the
/// copies retired by tombstones. A term is merged on its own, so memory stays
/// proportional to the longest posting list instead of the whole collection.
/// <para>
/// The new segment is written completely before the manifest names it: a crash
/// leaves either the old segments or the merged one, never a partial state. Old
/// directories become garbage that the next compaction removes.
/// </para>
/// </summary>
public sealed class SparseCollectionCompactor
{
    private readonly string _directoryPath;

    /// <summary>Postings per block in the merged segment.</summary>
    public int BlockSize { get; init; } = 64;

    public SparseCollectionCompactor(string collectionDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionDirectoryPath);
        _directoryPath = collectionDirectoryPath;
    }

    /// <summary>
    /// Merges all segments into one. Returns the new generation, or null when
    /// there was nothing to merge.
    /// </summary>
    public long? Compact() => Compact(null);

    /// <summary>
    /// Merges only the listed live segments into one new segment; the others
    /// stay as they are. The tombstones are kept, because they may still hide
    /// copies in the segments left out. They cannot hide anything in the merged
    /// segment: its generation is above every tombstone, and the copies they
    /// retire are dropped during the merge. Returns the new generation, or null
    /// when fewer than two of the listed segments are live.
    /// </summary>
    public long? CompactSegments(IEnumerable<long> generations)
    {
        ArgumentNullException.ThrowIfNull(generations);
        return Compact(new HashSet<long>(generations));
    }

    /// <summary>On-disk size of one segment of the collection.</summary>
    public static long SegmentSizeBytes(string collectionDirectoryPath, long generation)
    {
        var path = SparseCollectionWriter.SegmentPath(Path.Combine(collectionDirectoryPath, "segments"), generation);
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
            : 0;
    }

    private long? Compact(HashSet<long> selection)
    {
        var live = SparseCollectionManifest.Read(_directoryPath);
        var generations = selection is null ? live : live.Where(selection.Contains).ToList();
        var kept = selection is null ? [] : live.Where(generation => !selection.Contains(generation)).ToList();

        var segmentsPath = Path.Combine(_directoryPath, "segments");
        var segments = new List<OpenSegment>(generations.Count);
        var tombstones = new StoredList<SparseTombstone, ValueTuple>(
            new StoreListOptions { FilePath = Path.Combine(_directoryPath, "tombstones") }, default);
        try
        {
            // One segment is still worth rewriting when it carries tombstones:
            // the retired copies are inside it and nothing else would ever drop
            // them from disk. A partial merge needs two segments.
            if (generations.Count == 0 ||
                (generations.Count == 1 && (selection is not null || tombstones.Count == 0)))
            {
                RemoveSegmentDirectoriesNotIn(live);
                return null;
            }

            foreach (var generation in generations)
            {
                var directoryPath = SparseCollectionWriter.SegmentPath(segmentsPath, generation);
                var directory = new StoredList<SparseTermDirectoryEntry, ValueTuple>(
                    new StoreListOptions { FilePath = Path.Combine(directoryPath, "terms") }, default);
                var blocks = new StoredList<SparsePostingBlock, ValueTuple>(
                    new StoreListOptions { FilePath = Path.Combine(directoryPath, "blocks") }, default);
                segments.Add(new OpenSegment(generation, directory, blocks));
            }

            var retired = new TombstoneLookup(tombstones);
            var nextGeneration = live[^1] + 1;
            var targetPath = SparseCollectionWriter.SegmentPath(segmentsPath, nextGeneration);
            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath, recursive: true);
            Merge(segments, retired, targetPath, nextGeneration);

            List<long> published = [.. kept, nextGeneration];
            SparseCollectionManifest.Write(_directoryPath, published);

            if (selection is null)
                tombstones.Clear();
            foreach (var segment in segments)
                segment.Dispose();
            segments.Clear();
            RemoveSegmentDirectoriesNotIn(published);
            return nextGeneration;
        }
        finally
        {
            foreach (var segment in segments)
                segment.Dispose();
            tombstones.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void Merge(List<OpenSegment> segments, TombstoneLookup retired, string targetPath, long generation)
    {
        // Term identifiers across every segment, read from the directory records
        // only: the posting payloads are touched one term at a time below.
        var termIds = new SortedSet<int>();
        foreach (var segment in segments)
            for (var position = 0; position < segment.Directory.Count; position++)
            {
                var entry = segment.Directory[position];
                if (entry.BlockReferences.Length > 0)
                    termIds.Add(entry.TermId);
            }

        Directory.CreateDirectory(targetPath);
        var targetTerms = new StoredList<SparseTermDirectoryEntry, ValueTuple>(
            new StoreListOptions { FilePath = Path.Combine(targetPath, "terms") }, default);
        var targetBlocks = new StoredList<SparsePostingBlock, ValueTuple>(
            new StoreListOptions { FilePath = Path.Combine(targetPath, "blocks") }, default);
        try
        {
            foreach (var termId in termIds)
            {
                var cursors = new List<(long Generation, SparsePostingCursor Cursor)>();
                foreach (var segment in segments)
                    foreach (var position in segment.Positions(termId))
                        cursors.Add((segment.Generation,
                            new SparsePostingCursor(segment.Directory[position], segment.Blocks)));

                var references = MergeTerm(cursors, retired, targetBlocks);
                if (references.Count > 0)
                    targetTerms.Add(new SparseTermDirectoryEntry(termId, references));
            }

            targetTerms.Flush();
            targetBlocks.Flush();
        }
        finally
        {
            targetTerms.DisposeAsync().AsTask().GetAwaiter().GetResult();
            targetBlocks.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private List<SparseBlockReference> MergeTerm(List<(long Generation, SparsePostingCursor Cursor)> cursors,
        TombstoneLookup retired, StoredList<SparsePostingBlock, ValueTuple> targetBlocks)
    {
        var references = new List<SparseBlockReference>();
        var buffer = new List<SparsePosting>(BlockSize);

        // A fresh cursor holds no posting until it advances once.
        foreach (var cursor in cursors)
            cursor.Cursor.MoveNext();

        while (true)
        {
            var chosen = -1;
            for (var index = 0; index < cursors.Count; index++)
            {
                if (cursors[index].Cursor.Current is not { } current)
                    continue;
                if (chosen < 0 || StartsBefore(current, cursors[chosen].Cursor.Current!.Value))
                    chosen = index;
            }

            if (chosen < 0)
                break;

            var posting = cursors[chosen].Cursor.Current!.Value;
            cursors[chosen].Cursor.MoveNext();
            if (retired.Hides(posting.DocumentId, cursors[chosen].Generation))
                continue;

            buffer.Add(posting);
            if (buffer.Count == BlockSize)
                FlushBlock(buffer, references, targetBlocks);
        }

        if (buffer.Count > 0)
            FlushBlock(buffer, references, targetBlocks);
        return references;
    }

    private void FlushBlock(List<SparsePosting> buffer, List<SparseBlockReference> references,
        StoredList<SparsePostingBlock, ValueTuple> targetBlocks)
    {
        var block = new SparsePostingBlock(buffer.ToArray());
        references.Add(new SparseBlockReference(targetBlocks.Count, block));
        targetBlocks.Add(block);
        buffer.Clear();
    }

    private static bool StartsBefore(SparsePosting left, SparsePosting right)
    {
        var comparison = left.DocumentId.SequenceCompareTo(right.DocumentId);
        return comparison != 0 ? comparison < 0 : left.VectorIndex < right.VectorIndex;
    }

    // Directories left behind by an interrupted compaction are not in the
    // manifest, so they are invisible to readers and safe to delete.
    private void RemoveSegmentDirectoriesNotIn(IReadOnlyList<long> generations)
    {
        var segmentsPath = Path.Combine(_directoryPath, "segments");
        if (!Directory.Exists(segmentsPath))
            return;

        var kept = new HashSet<string>(generations.Select(generation =>
            SparseCollectionWriter.SegmentPath(segmentsPath, generation)), StringComparer.Ordinal);
        foreach (var directory in Directory.EnumerateDirectories(segmentsPath))
            if (!kept.Contains(directory))
                Directory.Delete(directory, recursive: true);
    }

    private sealed class OpenSegment(long generation,
        StoredList<SparseTermDirectoryEntry, ValueTuple> directory,
        StoredList<SparsePostingBlock, ValueTuple> blocks) : IDisposable
    {
        private readonly Dictionary<int, List<int>> _positions = BuildPositions(directory);

        public long Generation { get; } = generation;
        public StoredList<SparseTermDirectoryEntry, ValueTuple> Directory { get; } = directory;
        public StoredList<SparsePostingBlock, ValueTuple> Blocks { get; } = blocks;

        public IReadOnlyList<int> Positions(int termId) =>
            _positions.TryGetValue(termId, out var positions) ? positions : [];

        public void Dispose()
        {
            Directory.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Blocks.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private static Dictionary<int, List<int>> BuildPositions(
            StoredList<SparseTermDirectoryEntry, ValueTuple> directory)
        {
            var positions = new Dictionary<int, List<int>>();
            for (var position = 0; position < directory.Count; position++)
            {
                var termId = directory[position].TermId;
                if (!positions.TryGetValue(termId, out var list))
                    positions[termId] = list = [];
                list.Add(position);
            }

            return positions;
        }
    }

    private sealed class TombstoneLookup
    {
        private readonly Dictionary<byte[], long[]> _generations;

        public TombstoneLookup(StoredList<SparseTombstone, ValueTuple> tombstones)
        {
            var grouped = new Dictionary<byte[], List<long>>(SparseKeyComparer.Instance);
            for (var position = 0; position < tombstones.Count; position++)
            {
                var tombstone = tombstones[position];
                var key = tombstone.DocumentId.ToArray();
                if (!grouped.TryGetValue(key, out var list))
                    grouped[key] = list = [];
                list.Add(tombstone.UpToGeneration);
            }

            _generations = new Dictionary<byte[], long[]>(SparseKeyComparer.Instance);
            foreach (var (key, list) in grouped)
                _generations[key] = list.Order().ToArray();
        }

        public bool Hides(ReadOnlySpan<byte> documentId, long generation)
        {
            var key = documentId.ToArray();
            return _generations.TryGetValue(key, out var generations) && generations[^1] >= generation;
        }
    }
}
