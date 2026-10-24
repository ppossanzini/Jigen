using System.Buffers.Binary;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

public sealed class SparseTermDirectoryEntry : IStorableItem<SparseTermDirectoryEntry, ValueTuple>
{
    private const int SerializationVersion = 1;
    private const int HeaderSize = 3 * sizeof(int);

    private readonly SparseBlockReference[] _blocks;

    public int TermId { get; }
    public IReadOnlyList<SparseBlockReference> Blocks => _blocks;

    internal SparseBlockReference[] BlockReferences => _blocks;
    public float MinimumWeight { get; }
    public float MaximumWeight { get; }

    public SparseTermDirectoryEntry(int termId, IEnumerable<SparseBlockReference> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var references = blocks.ToArray();
        (MinimumWeight, MaximumWeight) = Validate(termId, references);
        TermId = termId;
        _blocks = references;
    }

    // Takes ownership of the array to avoid copying an entry that has just been deserialized.
    internal SparseTermDirectoryEntry(int termId, SparseBlockReference[] blocks)
    {
        (MinimumWeight, MaximumWeight) = Validate(termId, blocks);
        TermId = termId;
        _blocks = blocks;
    }

    private static (float Minimum, float Maximum) Validate(int termId, SparseBlockReference[] references)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(termId);
        var positions = new HashSet<int>();
        var minimum = 0f;
        var maximum = 0f;

        for (var index = 0; index < references.Length; index++)
        {
            var reference = references[index];
            if (reference is null)
                throw new ArgumentException("A block reference cannot be null.", nameof(references));
            if (!positions.Add(reference.BlockIndex))
                throw new ArgumentException("A posting block cannot be referenced twice.", nameof(references));
            if (index > 0 && !reference.Follows(references[index - 1]))
                throw new ArgumentException(
                    "Block intervals must be strictly ordered and nonoverlapping.", nameof(references));

            minimum = MathF.Min(minimum, reference.MinimumWeight);
            maximum = MathF.Max(maximum, reference.MaximumWeight);
        }

        return (minimum, maximum);
    }

    public ReadOnlyMemory<byte> Serialize()
    {
        var length = HeaderSize;
        foreach (var block in _blocks)
            length = checked(length + block.SerializedSize);

        var buffer = new byte[length];
        var destination = buffer.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(destination, SerializationVersion);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], TermId);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], _blocks.Length);

        var offset = HeaderSize;
        foreach (var block in _blocks)
        {
            block.WriteTo(destination[offset..]);
            offset += block.SerializedSize;
        }

        return buffer;
    }

    public static SparseTermDirectoryEntry Deserialize(ReadOnlyMemory<byte> data, ValueTuple options)
    {
        var source = data.Span;
        if (source.Length < HeaderSize)
            throw new InvalidDataException("Truncated sparse term directory header.");

        var version = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (version != SerializationVersion)
            throw new InvalidDataException($"Unsupported sparse term directory version: {version}.");
        var termId = BinaryPrimitives.ReadInt32LittleEndian(source[4..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(source[8..]);
        if (termId < 0 || count < 0 || count > (source.Length - HeaderSize) / SparseBlockReference.MinimumSerializedSize)
            throw new InvalidDataException("Invalid sparse term identifier or block count.");

        source = source[HeaderSize..];
        var references = new SparseBlockReference[count];
        var positions = new HashSet<int>();
        for (var index = 0; index < count; index++)
        {
            var reference = SparseBlockReference.ReadFrom(ref source);
            if (!positions.Add(reference.BlockIndex))
                throw new InvalidDataException("A posting block is referenced twice.");
            if (index > 0 && !reference.Follows(references[index - 1]))
                throw new InvalidDataException("Sparse block intervals overlap or are not ordered.");
            references[index] = reference;
        }

        if (!source.IsEmpty)
            throw new InvalidDataException("Unexpected trailing data in sparse term directory entry.");

        return new SparseTermDirectoryEntry(termId, references);
    }
}
