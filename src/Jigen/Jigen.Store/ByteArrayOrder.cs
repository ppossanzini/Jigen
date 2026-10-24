namespace Jigen;

/// <summary>Orders binary keys by their bytes, for stable tie-breaking.</summary>
internal sealed class ByteArrayOrder : IComparer<byte[]>
{
    public static ByteArrayOrder Instance { get; } = new();

    public int Compare(byte[] left, byte[] right) =>
        left is null
            ? right is null ? 0 : -1
            : right is null ? 1 : left.AsSpan().SequenceCompareTo(right);
}
