using System.Buffers.Binary;

namespace Jigen.Index.Sparse;

public sealed class SparseBlockReference
{
    internal const int FixedSize = 8 * sizeof(int);
    internal const int MinimumSerializedSize = FixedSize + 2;

    private readonly byte[] _firstDocumentId;
    private readonly byte[] _lastDocumentId;

    public int BlockIndex { get; }
    public int PostingCount { get; }
    public float MinimumWeight { get; }
    public float MaximumWeight { get; }
    public ReadOnlySpan<byte> FirstDocumentId => _firstDocumentId;
    public int FirstVectorIndex { get; }
    public ReadOnlySpan<byte> LastDocumentId => _lastDocumentId;
    public int LastVectorIndex { get; }

    internal int SerializedSize => checked(FixedSize + _firstDocumentId.Length + _lastDocumentId.Length);
    internal SparsePosting LastPosting =>
        new(_lastDocumentId, 0, _lastDocumentId.Length, LastVectorIndex, 0);

    public SparseBlockReference(int blockIndex, SparsePostingBlock block)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        ArgumentNullException.ThrowIfNull(block);
        if (block.Postings.Count == 0)
            throw new ArgumentException("An empty posting block has no document interval.", nameof(block));

        BlockIndex = blockIndex;
        PostingCount = block.Postings.Count;
        MinimumWeight = block.MinimumWeight;
        MaximumWeight = block.MaximumWeight;
        _firstDocumentId = block.Postings[0].DocumentId.ToArray();
        FirstVectorIndex = block.Postings[0].VectorIndex;
        _lastDocumentId = block.Postings[^1].DocumentId.ToArray();
        LastVectorIndex = block.Postings[^1].VectorIndex;
    }

    private SparseBlockReference(int blockIndex, int postingCount, float minimumWeight,
        float maximumWeight, byte[] firstDocumentId, int firstVectorIndex,
        byte[] lastDocumentId, int lastVectorIndex)
    {
        BlockIndex = blockIndex;
        PostingCount = postingCount;
        MinimumWeight = minimumWeight;
        MaximumWeight = maximumWeight;
        _firstDocumentId = firstDocumentId;
        FirstVectorIndex = firstVectorIndex;
        _lastDocumentId = lastDocumentId;
        LastVectorIndex = lastVectorIndex;
    }

    internal bool Follows(SparseBlockReference previous)
    {
        var comparison = FirstDocumentId.SequenceCompareTo(previous.LastDocumentId);
        return comparison > 0 || comparison == 0 && FirstVectorIndex > previous.LastVectorIndex;
    }

    internal void WriteTo(Span<byte> destination)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, BlockIndex);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], PostingCount);
        BinaryPrimitives.WriteSingleLittleEndian(destination[8..], MinimumWeight);
        BinaryPrimitives.WriteSingleLittleEndian(destination[12..], MaximumWeight);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], FirstVectorIndex);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], LastVectorIndex);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], _firstDocumentId.Length);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..], _lastDocumentId.Length);
        _firstDocumentId.CopyTo(destination[FixedSize..]);
        _lastDocumentId.CopyTo(destination[(FixedSize + _firstDocumentId.Length)..]);
    }

    internal static SparseBlockReference ReadFrom(ref ReadOnlySpan<byte> source)
    {
        if (source.Length < MinimumSerializedSize)
            throw new InvalidDataException("Truncated sparse block reference.");

        var blockIndex = BinaryPrimitives.ReadInt32LittleEndian(source);
        var postingCount = BinaryPrimitives.ReadInt32LittleEndian(source[4..]);
        var minimum = BinaryPrimitives.ReadSingleLittleEndian(source[8..]);
        var maximum = BinaryPrimitives.ReadSingleLittleEndian(source[12..]);
        var firstVectorIndex = BinaryPrimitives.ReadInt32LittleEndian(source[16..]);
        var lastVectorIndex = BinaryPrimitives.ReadInt32LittleEndian(source[20..]);
        var firstLength = BinaryPrimitives.ReadInt32LittleEndian(source[24..]);
        var lastLength = BinaryPrimitives.ReadInt32LittleEndian(source[28..]);

        if (blockIndex < 0 || postingCount <= 0 || firstVectorIndex < 0 || lastVectorIndex < 0)
            throw new InvalidDataException("Invalid sparse block position, count or vector index.");
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum > 0 || maximum < 0)
            throw new InvalidDataException("Invalid sparse block weight bounds.");
        if (firstLength <= 0 || lastLength <= 0 ||
            firstLength > source.Length - FixedSize ||
            lastLength > source.Length - FixedSize - firstLength)
            throw new InvalidDataException("Invalid sparse block document identifier lengths.");

        var firstDocumentId = source.Slice(FixedSize, firstLength);
        var lastDocumentId = source.Slice(FixedSize + firstLength, lastLength);
        var comparison = firstDocumentId.SequenceCompareTo(lastDocumentId);
        if (comparison == 0)
            comparison = firstVectorIndex.CompareTo(lastVectorIndex);
        if (postingCount == 1 ? comparison != 0 : comparison >= 0)
            throw new InvalidDataException("Invalid sparse block document interval.");
        if (postingCount == 1 && minimum < 0 && maximum > 0)
            throw new InvalidDataException("A single posting cannot have both negative and positive weights.");

        var reference = new SparseBlockReference(blockIndex, postingCount, minimum, maximum,
            firstDocumentId.ToArray(), firstVectorIndex, lastDocumentId.ToArray(), lastVectorIndex);
        source = source[(FixedSize + firstLength + lastLength)..];
        return reference;
    }
}
