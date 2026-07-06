using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Persistance;

namespace Jigen.Indexer;

public class IndexNode : IStorableItem<IndexNode, SmallWorldOptions>
{
  private const byte SerializationVersion = 1;

  public int PositionId { get; set; }
  public VectorKey Id { get; set; }
  public bool IsDeleted { get; set; }
  public int MaxLevel { get; set; }

  private float[] _vector = [];

  // Disk-backed nodes read their vector straight from the mapped vector file:
  // no deserialization, no float[] per access. _vector doubles as the staging
  // copy for freshly inserted nodes until a remap covers their offset.
  internal MappedVectorFile MappedVectors;
  internal long MappedFloatOffset;
  internal int MappedDimensions;

  /// <summary>
  /// The vector as a span, without materializing it: RAM copy when present,
  /// otherwise zero-copy from the mapped vector file. Use this (or
  /// <see cref="VectorDimensions"/>) on hot paths instead of <see cref="Vector"/>.
  /// </summary>
  public ReadOnlySpan<float> VectorSpan
  {
    get
    {
      var vector = _vector;
      if (vector.Length > 0 || MappedVectors is null) return vector;
      return MappedVectors.Floats(MappedFloatOffset, MappedDimensions);
    }
  }

  public int VectorDimensions
  {
    get
    {
      var vector = _vector;
      if (vector.Length > 0 || MappedVectors is null) return vector.Length;
      return MappedDimensions;
    }
  }

  /// <summary>
  /// Materialized vector. For mapped nodes the getter COPIES from the map:
  /// cold paths only (serialization, migration, custom distance functions).
  /// </summary>
  public float[] Vector
  {
    get
    {
      var vector = _vector;
      if (vector.Length > 0 || MappedVectors is null) return vector;
      return VectorSpan.ToArray();
    }
    set => _vector = value ?? [];
  }

  /// <summary>Drops the RAM staging copy once the mapping covers the offset.</summary>
  internal void ReleaseRamVector() => _vector = [];

  public IList<IList<int>> Connections { get; set; } = Array.Empty<IList<int>>();
  public TravelingCosts TravelingCosts { get; private set; }

  public IndexNode(SmallWorldOptions options)
  {
    TravelingCosts = new TravelingCosts(this, options);
  }

  public ReadOnlyMemory<byte> Serialize()
  {
    if (PositionId < 0) throw new InvalidOperationException("PositionId must be non-negative.");
    if (MaxLevel < 0) throw new InvalidOperationException("MaxLevel must be non-negative.");
    if (Id.Value is null) throw new InvalidOperationException("Id.Value cannot be null.");

    var writer = new ArrayBufferWriter<byte>();

    writer.WriteByte(SerializationVersion);
    writer.WriteLEInt(PositionId);

    writer.WriteByte((byte)(IsDeleted ? 1 : 0));

    writer.WriteLEInt(Id.Value.Length);
    writer.WriteBytes(Id.Value);

    writer.WriteLEInt(Vector.Length);
    if (Vector.Length > 0)
      writer.WriteBytes(MemoryMarshal.AsBytes<float>(Vector.AsSpan()));

    writer.WriteLEInt(MaxLevel);
    writer.WriteLEInt(Connections.Count);

    for (int level = 0; level < Connections.Count; level++)
    {
      var levelConnections = Connections[level] ?? Array.Empty<int>();
      var ordered = new int[levelConnections.Count];
      for (int i = 0; i < levelConnections.Count; i++)
      {
        var connection = levelConnections[i];
        if (connection < 0) throw new InvalidOperationException("Connections values must be non-negative.");
        ordered[i] = connection;
      }

      Array.Sort(ordered);
      writer.WriteLEInt(ordered.Length);

      foreach (var current in ordered)
        writer.WriteLEInt(current);
    }

    return writer.WrittenMemory;
  }

  public static IndexNode Deserialize(ReadOnlyMemory<byte> data, SmallWorldOptions options)
  {
    var span = data.Span;
    int offset = 0;

    var version = span.ReadByte(ref offset);
    if (version != SerializationVersion)
      throw new InvalidDataException($"Unsupported IndexNode serialization version: {version}");

    var positionId = span.ReadLEInt32(ref offset);

    var isDeleted = span.ReadByte(ref offset) == 1;

    var idLength = span.ReadLEInt32(ref offset);
    if (idLength < 0 || offset + idLength > span.Length)
      throw new InvalidDataException("Invalid Id length in serialized IndexNode payload.");

    var idBuffer = span.Slice(offset, idLength).ToArray();
    offset += idLength;

    var vectorLength = span.ReadLEInt32(ref offset);
    if (vectorLength < 0)
      throw new InvalidDataException("Invalid vector length in serialized IndexNode payload.");

    var vectorBytesLength = checked(vectorLength * sizeof(float));
    if (offset + vectorBytesLength > span.Length)
      throw new InvalidDataException("Invalid vector payload length in serialized IndexNode payload.");

    var vector = vectorLength == 0
      ? Array.Empty<float>()
      : MemoryMarshal.Cast<byte, float>(span.Slice(offset, vectorBytesLength)).ToArray();
    offset += vectorBytesLength;

    var maxLevel = span.ReadLEInt32(ref offset);
    var levelsCount = span.ReadLEInt32(ref offset);
    if (levelsCount < 0)
      throw new InvalidDataException("Invalid levels count in serialized IndexNode payload.");

    var connections = new IList<int>[levelsCount];
    for (int level = 0; level < levelsCount; level++)
    {
      var connCount = span.ReadLEInt32(ref offset);
      if (connCount < 0)
        throw new InvalidDataException("Invalid connection count in serialized IndexNode payload.");

      var levelConnections = new int[connCount];
      for (int i = 0; i < connCount; i++)
      {
        var val = span.ReadLEInt32(ref offset);
        levelConnections[i] = val;
      }

      connections[level] = levelConnections;
    }

    if (offset != span.Length)
      throw new InvalidDataException("Unexpected trailing bytes in serialized IndexNode payload.");

    var node = new IndexNode(options)
    {
      PositionId = positionId,
      IsDeleted = isDeleted,
      Id = idBuffer,
      Vector = vector,
      MaxLevel = maxLevel,
      Connections = connections
    };

    return node;
  }
}