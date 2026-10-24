using System.Buffers.Binary;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Retires the copies of a document written up to a generation. Every copy in a
/// segment with generation at or below <see cref="UpToGeneration"/> is hidden;
/// a version written after that stays live, so an update retires the previous
/// copy without hiding the new one.
/// </summary>
public sealed class SparseTombstone : IStorableItem<SparseTombstone, ValueTuple>
{
    private const int SerializationVersion = 1;

    private readonly byte[] _documentId;

    public ReadOnlySpan<byte> DocumentId => _documentId;
    public long UpToGeneration { get; }

    public SparseTombstone(ReadOnlySpan<byte> documentId, long upToGeneration)
    {
        if (documentId.IsEmpty)
            throw new ArgumentException("A tombstone requires a document identifier.", nameof(documentId));
        if (upToGeneration < 1)
            throw new ArgumentOutOfRangeException(nameof(upToGeneration),
                "A tombstone needs the last generation it retires.");
        _documentId = documentId.ToArray();
        UpToGeneration = upToGeneration;
    }

    public ReadOnlyMemory<byte> Serialize()
    {
        var buffer = new byte[2 * sizeof(int) + sizeof(long) + _documentId.Length];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, SerializationVersion);
        BinaryPrimitives.WriteInt64LittleEndian(span[sizeof(int)..], UpToGeneration);
        BinaryPrimitives.WriteInt32LittleEndian(span[(sizeof(int) + sizeof(long))..], _documentId.Length);
        _documentId.CopyTo(span[(2 * sizeof(int) + sizeof(long))..]);
        return buffer;
    }

    public static SparseTombstone Deserialize(ReadOnlyMemory<byte> data, ValueTuple options)
    {
        var span = data.Span;
        const int headerSize = 2 * sizeof(int) + sizeof(long);
        if (span.Length < headerSize)
            throw new InvalidDataException("Truncated sparse tombstone.");

        var version = BinaryPrimitives.ReadInt32LittleEndian(span);
        if (version != SerializationVersion)
            throw new InvalidDataException($"Unsupported sparse tombstone version: {version}.");
        var generation = BinaryPrimitives.ReadInt64LittleEndian(span[sizeof(int)..]);
        var length = BinaryPrimitives.ReadInt32LittleEndian(span[(sizeof(int) + sizeof(long))..]);
        if (generation < 1 || length <= 0 || length != span.Length - headerSize)
            throw new InvalidDataException("Invalid sparse tombstone.");

        return new SparseTombstone(span[headerSize..], generation);
    }
}
