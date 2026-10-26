namespace Jigen.Index.Sparse;

/// <summary>
/// Chooses which segments of a collection to merge, by size and not by count.
/// <para>
/// A segment that reached <c>maxSegmentBytes</c> is never merged again. The
/// smaller ones are merged from the newest backwards while the newer segments
/// together are at least as large as the next older one, and the result stays
/// within <c>maxSegmentBytes</c>. Segments merge with peers of similar size, so
/// each byte is rewritten a logarithmic number of times instead of at every
/// commit. Below <c>minSegmentBytes</c> a segment counts as that size: tiny
/// segments therefore merge as soon as a second one appears.
/// </para>
/// </summary>
public static class SparseMergePolicy
{
    /// <summary>
    /// Generations to merge together, oldest first, or an empty list when no
    /// merge is due. <paramref name="segments"/> must be ordered by generation.
    /// </summary>
    public static IReadOnlyList<long> Select(IReadOnlyList<(long Generation, long Bytes)> segments,
        long minSegmentBytes, long maxSegmentBytes)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (maxSegmentBytes <= 0)
            return [];

        var candidates = segments.Where(segment => segment.Bytes < maxSegmentBytes).ToList();
        if (candidates.Count < 2)
            return [];

        long Weight(long bytes) => Math.Max(bytes, minSegmentBytes);

        var newest = candidates[^1];
        var tailWeight = Weight(newest.Bytes);
        var tailBytes = newest.Bytes;
        var start = candidates.Count - 1;
        for (var index = candidates.Count - 2; index >= 0; index--)
        {
            var older = candidates[index];
            if (tailWeight < Weight(older.Bytes) || tailBytes + older.Bytes > maxSegmentBytes)
                break;
            tailWeight += Weight(older.Bytes);
            tailBytes += older.Bytes;
            start = index;
        }

        return start == candidates.Count - 1
            ? []
            : candidates.Skip(start).Select(segment => segment.Generation).ToArray();
    }
}
