using System.Buffers.Binary;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

public sealed class SparsePostingBlock : IStorableItem<SparsePostingBlock, ValueTuple>
{
    private const int SerializationVersion = 1;
    private const int HeaderSize = 4 * sizeof(int);
    private const int PostingFixedSize = 2 * sizeof(int) + sizeof(float);

    private readonly SparsePosting[] _postings;

    public IReadOnlyList<SparsePosting> Postings => _postings;

    internal SparsePosting[] PostingItems => _postings;

    // Missing postings contribute zero, including when query weights are negative.
    public float MinimumWeight { get; }
    public float MaximumWeight { get; }

    public SparsePostingBlock(IEnumerable<SparsePosting> postings)
    {
        ArgumentNullException.ThrowIfNull(postings);
        var items = postings.ToArray();
        (MinimumWeight, MaximumWeight) = Validate(items);
        _postings = items;
    }

    // Takes ownership of the array to avoid copying a block that has just been deserialized.
    internal SparsePostingBlock(SparsePosting[] postings)
    {
        (MinimumWeight, MaximumWeight) = Validate(postings);
        _postings = postings;
    }

    private static (float Minimum, float Maximum) Validate(SparsePosting[] items)
    {
        var minimum = 0f;
        var maximum = 0f;

        for (var index = 0; index < items.Length; index++)
        {
            var posting = items[index];
            if (posting.DocumentId.IsEmpty)
                throw new ArgumentException("A posting requires a document identifier.", nameof(items));
            if (index > 0 && CompareIdentity(items[index - 1], posting) >= 0)
                throw new ArgumentException(
                  "Postings must be strictly ordered by document identifier and vector index.", nameof(items));

            minimum = MathF.Min(minimum, posting.Weight);
            maximum = MathF.Max(maximum, posting.Weight);
        }

        return (minimum, maximum);
    }

    public ReadOnlyMemory<byte> Serialize()
    {
        var length = HeaderSize;
        foreach (var posting in _postings)
            length = checked(length + PostingFixedSize + posting.DocumentId.Length);

        var buffer = new byte[length];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, SerializationVersion);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], _postings.Length);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..], MinimumWeight);
        BinaryPrimitives.WriteSingleLittleEndian(span[12..], MaximumWeight);

        var offset = HeaderSize;
        foreach (var posting in _postings)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], posting.DocumentId.Length);
            offset += sizeof(int);
            posting.DocumentId.CopyTo(span[offset..]);
            offset += posting.DocumentId.Length;
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], posting.VectorIndex);
            offset += sizeof(int);
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], posting.Weight);
            offset += sizeof(float);
        }

        return buffer;
    }

    public static SparsePostingBlock Deserialize(ReadOnlyMemory<byte> data, ValueTuple options)
    {
        var span = data.Span;
        if (span.Length < HeaderSize)
            throw new InvalidDataException("Truncated sparse posting block header.");

        var version = BinaryPrimitives.ReadInt32LittleEndian(span);
        if (version != SerializationVersion)
            throw new InvalidDataException($"Unsupported sparse posting block version: {version}.");

        var count = BinaryPrimitives.ReadInt32LittleEndian(span[4..]);
        if (count < 0 || count > (span.Length - HeaderSize) / (PostingFixedSize + 1))
            throw new InvalidDataException("Invalid sparse posting count.");

        var minimum = BinaryPrimitives.ReadSingleLittleEndian(span[8..]);
        var maximum = BinaryPrimitives.ReadSingleLittleEndian(span[12..]);
        var postings = new SparsePosting[count];
        var documentBuffer = new byte[span.Length - HeaderSize - count * PostingFixedSize];
        var documentOffset = 0;
        var offset = HeaderSize;

        for (var index = 0; index < count; index++)
        {
            if (span.Length - offset < PostingFixedSize + 1)
                throw new InvalidDataException("Truncated sparse posting.");

            var idLength = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += sizeof(int);
            if (idLength <= 0 || idLength > span.Length - offset - sizeof(int) - sizeof(float))
                throw new InvalidDataException("Invalid sparse posting document identifier length.");

            var documentId = span.Slice(offset, idLength);
            offset += idLength;
            var vectorIndex = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += sizeof(int);
            var weight = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]);
            offset += sizeof(float);
            if (vectorIndex < 0 || !float.IsFinite(weight))
                throw new InvalidDataException("Invalid sparse posting vector index or weight.");

            documentId.CopyTo(documentBuffer.AsSpan(documentOffset, idLength));
            postings[index] = new SparsePosting(documentBuffer, documentOffset, idLength,
                vectorIndex, weight);
            documentOffset += idLength;
            if (index > 0 && CompareIdentity(postings[index - 1], postings[index]) >= 0)
                throw new InvalidDataException("Sparse postings are not strictly ordered.");
        }

        if (offset != span.Length || documentOffset != documentBuffer.Length)
            throw new InvalidDataException("Unexpected trailing data in sparse posting block.");

        var block = new SparsePostingBlock(postings);
        if (minimum != block.MinimumWeight || maximum != block.MaximumWeight)
            throw new InvalidDataException("Sparse posting block bounds do not match its weights.");

        return block;
    }

    private static int CompareIdentity(SparsePosting left, SparsePosting right)
    {
        var comparison = left.DocumentId.SequenceCompareTo(right.DocumentId);
        return comparison != 0 ? comparison : left.VectorIndex.CompareTo(right.VectorIndex);
    }
}
