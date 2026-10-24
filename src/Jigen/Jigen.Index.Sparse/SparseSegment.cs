namespace Jigen.Index.Sparse;

/// <summary>
/// One immutable index of a collection: its own term directory and posting
/// blocks, written in one pass and never modified afterwards. A newer segment
/// supersedes nothing by itself; a tombstone retires the documents it replaced.
/// </summary>
public sealed class SparseSegment
{
    /// <summary>Position of this segment in the collection's evolution.</summary>
    public long Generation { get; }

    /// <summary>Read-only search over this segment alone.</summary>
    public SparseIndex Index { get; }

    public SparseSegment(long generation, SparseIndex index)
    {
        if (generation < 1)
            throw new ArgumentOutOfRangeException(nameof(generation), "A segment needs a positive generation.");
        ArgumentNullException.ThrowIfNull(index);
        Generation = generation;
        Index = index;
    }
}
