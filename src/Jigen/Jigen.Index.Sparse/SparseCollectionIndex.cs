namespace Jigen.Index.Sparse;

/// <summary>
/// Search over a collection made of several immutable segments.
/// <para>
/// A document lives in at most one live segment, because replacing or deleting
/// it records a tombstone, so the segments never hold the same key twice. That
/// is what makes the merge exact: the best k documents of the whole collection
/// are among the best k of each segment.
/// </para>
/// </summary>
public sealed class SparseCollectionIndex
{
    private readonly SparseSegment[] _segments;
    // Sorted by key so a visibility check is a binary search with no allocation.
    private readonly byte[][] _retiredKeys;
    private readonly long[][] _retiredGenerations;

    public SparseCollectionIndex(IReadOnlyList<SparseSegment> segments, IEnumerable<SparseTombstone> tombstones)
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = [.. segments.OrderBy(segment => segment.Generation)];

        var grouped = (tombstones ?? [])
            .GroupBy(tombstone => tombstone.DocumentId.ToArray(), SparseKeyComparer.Instance)
            .Select(group => (Key: group.Key, Generations: group.Select(item => item.UpToGeneration)
                .Order().ToArray()))
            .OrderBy(item => item.Key, SparseKeyComparer.Instance)
            .ToArray();
        _retiredKeys = grouped.Select(item => item.Key).ToArray();
        _retiredGenerations = grouped.Select(item => item.Generations).ToArray();
    }

    /// <summary>Live segments, oldest first.</summary>
    public IReadOnlyList<SparseSegment> Segments => _segments;

    /// <summary>Number of tombstone records applied to this collection.</summary>
    public int TombstoneCount => _retiredGenerations.Sum(generations => generations.Length);

    /// <summary>
    /// Best k documents of the whole collection: every segment is searched and
    /// the results are merged by descending score then ascending key.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> Search(IReadOnlyDictionary<int, float> query, int k,
        CancellationToken cancellationToken = default) =>
        Search(query, k, null, cancellationToken);

    /// <summary>
    /// Same search, skipping the documents the filter rejects. The filter applies
    /// to the live copies, so a document retired in the collection never reaches
    /// the caller even when an older segment still holds it.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> Search(IReadOnlyDictionary<int, float> query, int k,
        SparseDocumentFilter filter, CancellationToken cancellationToken = default) =>
        Search(query, k, filter, double.NegativeInfinity, cancellationToken);

    /// <summary>
    /// Same search, keeping only the documents that reach
    /// <paramref name="minimumScore"/>.
    /// </summary>
    public IReadOnlyList<SparseSearchResult> Search(IReadOnlyDictionary<int, float> query, int k,
        SparseDocumentFilter filter, double minimumScore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        if (k == 0 || _segments.Length == 0)
            return Array.Empty<SparseSearchResult>();

        var merged = new List<SparseSearchResult>();
        foreach (var segment in _segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = segment.Generation;
            merged.AddRange(segment.Index.Search(query, k,
                documentId => !IsRetiredIn(documentId, generation) && (filter is null || filter(documentId)),
                minimumScore, cancellationToken));
        }

        merged.Sort(CompareByRank);
        if (merged.Count > k)
            merged.RemoveRange(k, merged.Count - k);
        return merged;
    }

    private static int CompareByRank(SparseSearchResult left, SparseSearchResult right)
    {
        var comparison = right.Score.CompareTo(left.Score);
        return comparison != 0 ? comparison : left.DocumentId.SequenceCompareTo(right.DocumentId);
    }

    // A copy is hidden when it was written at or before the generation the
    // tombstone covers, so a later version of the same document stays visible.
    private bool IsRetiredIn(ReadOnlySpan<byte> documentId, long generation)
    {
        var lower = 0;
        var upper = _retiredKeys.Length;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var comparison = _retiredKeys[middle].AsSpan().SequenceCompareTo(documentId);
            if (comparison < 0)
                lower = middle + 1;
            else if (comparison > 0)
                upper = middle;
            else
                // Generations are sorted: the largest one decides.
                return _retiredGenerations[middle][^1] >= generation;
        }

        return false;
    }
}
