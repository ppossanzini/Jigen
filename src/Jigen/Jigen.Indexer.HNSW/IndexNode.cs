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
  public bool IsDeleted { get; set; } = false;
  public int MaxLevel { get; set; }
  public float[] Vector { get; set; } = Array.Empty<float>();


  public List<IList<int>> Connections { get; init; } = new();
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
    writer.WriteVarUInt((uint)PositionId);

    writer.WriteByte((byte)(IsDeleted ? 1 : 0));

    writer.WriteVarUInt((uint)Id.Value.Length);
    writer.WriteBytes(Id.Value);

    writer.WriteVarUInt((uint)Vector.Length);
    if (Vector.Length > 0)
      writer.WriteBytes(MemoryMarshal.AsBytes<float>(Vector.AsSpan()));

    writer.WriteVarUInt((uint)MaxLevel);
    writer.WriteVarUInt((uint)Connections.Count);

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
      writer.WriteVarUInt((uint)ordered.Length);

      uint previous = 0;
      for (int i = 0; i < ordered.Length; i++)
      {
        uint current = (uint)ordered[i];
        uint delta = i == 0 ? current : current - previous;
        writer.WriteVarUInt(delta);
        previous = current;
      }
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

    var positionId = checked((int)span.ReadVarUInt(ref offset));
    
    var isDeleted = span.ReadByte(ref offset) == 1;

    var idLength = checked((int)span.ReadVarUInt(ref offset));
    if (idLength < 0 || offset + idLength > span.Length)
      throw new InvalidDataException("Invalid Id length in serialized IndexNode payload.");

    var idBuffer = span.Slice(offset, idLength).ToArray();
    offset += idLength;

    var vectorLength = checked((int)span.ReadVarUInt(ref offset));
    if (vectorLength < 0)
      throw new InvalidDataException("Invalid vector length in serialized IndexNode payload.");

    var vectorBytesLength = checked(vectorLength * sizeof(float));
    if (offset + vectorBytesLength > span.Length)
      throw new InvalidDataException("Invalid vector payload length in serialized IndexNode payload.");

    var vector = vectorLength == 0
      ? Array.Empty<float>()
      : MemoryMarshal.Cast<byte, float>(span.Slice(offset, vectorBytesLength)).ToArray();
    offset += vectorBytesLength;

    var maxLevel = checked((int)span.ReadVarUInt(ref offset));
    var levelsCount = checked((int)span.ReadVarUInt(ref offset));
    if (levelsCount < 0)
      throw new InvalidDataException("Invalid levels count in serialized IndexNode payload.");

    var connections = new List<IList<int>>(levelsCount);
    for (int level = 0; level < levelsCount; level++)
    {
      var connCount = checked((int)span.ReadVarUInt(ref offset));
      if (connCount < 0)
        throw new InvalidDataException("Invalid connection count in serialized IndexNode payload.");

      var levelConnections = new List<int>(connCount);
      uint previous = 0;
      for (int i = 0; i < connCount; i++)
      {
        var delta = span.ReadVarUInt(ref offset);
        uint current = i == 0 ? delta : checked(previous + delta);
        levelConnections.Add(checked((int)current));
        previous = current;
      }

      connections.Add(levelConnections);
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