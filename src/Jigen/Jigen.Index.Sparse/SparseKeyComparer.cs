namespace Jigen.Index.Sparse;

/// <summary>Orders and compares binary document keys by their bytes.</summary>
internal sealed class SparseKeyComparer : IComparer<byte[]>, IEqualityComparer<byte[]>
{
    public static SparseKeyComparer Instance { get; } = new();

    public int Compare(byte[] left, byte[] right) =>
        left is null
            ? right is null ? 0 : -1
            : right is null ? 1 : left.AsSpan().SequenceCompareTo(right);

    public bool Equals(byte[] left, byte[] right) => Compare(left, right) == 0;

    public int GetHashCode(byte[] value) =>
        value is null ? 0 : unchecked((int)System.IO.Hashing.XxHash32.HashToUInt32(value));
}
