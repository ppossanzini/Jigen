using System.Buffers.Binary;
using System.Collections;
using System.Runtime.InteropServices;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;

namespace Jigen.Indexer;

/// <summary>
/// Immutable half of a graph node: written once when the node is inserted and
/// never rewritten. Keeping the vector out of the mutable record removes the
/// dominant write-amplification of graph construction (every insert used to
/// rewrite the FULL records of ~M neighbours, vector included).
/// </summary>
internal sealed class VectorPart : IStorableItem<VectorPart, SmallWorldOptions>
{
  // Version 1: float payload. Version 2: SQ8 payload (sbyte per component).
  // Same header, so the mapped-read path only branches on the payload stride.
  internal const byte FloatVersion = 1;
  internal const byte Sq8Version = 2;

  public VectorKey Id { get; init; }
  public float[] Vector { get; init; } = [];
  public sbyte[] QuantizedVector { get; init; }
  public int MaxLevel { get; init; }

  public ReadOnlyMemory<byte> Serialize()
  {
    var id = Id.Value ?? [];
    var quantized = QuantizedVector;
    var components = quantized?.Length ?? Vector.Length;
    var payloadBytes = quantized is not null ? components : components * sizeof(float);

    var size = 1 + sizeof(int) + id.Length + sizeof(int) + sizeof(int) + payloadBytes;
    var buffer = new byte[size];

    var offset = 0;
    buffer[offset++] = quantized is not null ? Sq8Version : FloatVersion;
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), id.Length);
    offset += sizeof(int);
    id.CopyTo(buffer.AsSpan(offset));
    offset += id.Length;
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), MaxLevel);
    offset += sizeof(int);
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), components);
    offset += sizeof(int);

    if (quantized is not null)
      MemoryMarshal.AsBytes<sbyte>(quantized).CopyTo(buffer.AsSpan(offset));
    else
      MemoryMarshal.AsBytes(Vector.AsSpan()).CopyTo(buffer.AsSpan(offset));

    return buffer;
  }

  public static VectorPart Deserialize(ReadOnlyMemory<byte> data, SmallWorldOptions options)
  {
    var span = data.Span;
    var offset = 0;

    var version = span.ReadByte(ref offset);
    if (version != FloatVersion && version != Sq8Version)
      throw new InvalidDataException($"Unsupported VectorPart serialization version: {version}");

    var idLength = span.ReadLEInt32(ref offset);
    if (idLength < 0 || offset + idLength > span.Length)
      throw new InvalidDataException("Invalid Id length in serialized VectorPart payload.");
    var id = span.Slice(offset, idLength).ToArray();
    offset += idLength;

    var maxLevel = span.ReadLEInt32(ref offset);

    var components = span.ReadLEInt32(ref offset);
    var payloadBytes = checked(components * (version == Sq8Version ? 1 : sizeof(float)));
    if (components < 0 || offset + payloadBytes > span.Length)
      throw new InvalidDataException("Invalid vector length in serialized VectorPart payload.");

    if (version == Sq8Version)
    {
      var quantized = MemoryMarshal.Cast<byte, sbyte>(span.Slice(offset, payloadBytes)).ToArray();
      return new VectorPart { Id = id, QuantizedVector = quantized, MaxLevel = maxLevel };
    }

    var vector = components == 0
      ? []
      : MemoryMarshal.Cast<byte, float>(span.Slice(offset, payloadBytes)).ToArray();

    return new VectorPart { Id = id, Vector = vector, MaxLevel = maxLevel };
  }
}

/// <summary>
/// Mutable half of a graph node: deletion flag, adjacency lists and (for slot 0
/// only) the entrypoint pointer. Each level is serialized with its CAPACITY
/// (max connections for that level) and padded to it, so the record size never
/// grows while wiring the graph: every update is an in-place overwrite, no
/// relocations, no dead space, no file growth.
/// </summary>
internal sealed class AdjacencyPart : IStorableItem<AdjacencyPart, SmallWorldOptions>
{
  private const byte SerializationVersion = 1;
  private readonly SmallWorldOptions _options;

  public AdjacencyPart(SmallWorldOptions options)
  {
    _options = options;
  }

  /// <summary>PositionId of the graph entrypoint; meaningful for slot 0 only.</summary>
  public int EntryPointer { get; init; }

  public bool IsDeleted { get; init; }
  public IList<IList<int>> Connections { get; init; } = Array.Empty<IList<int>>();

  public ReadOnlyMemory<byte> Serialize()
  {
    var levels = Connections?.Count ?? 0;

    // Header + per level: [capacity][count][capacity * ids].
    var size = 1 + sizeof(int) + 1 + sizeof(int);
    var capacities = new int[levels];
    for (var level = 0; level < levels; level++)
    {
      // The construction pruning keeps counts within GetM; max() keeps the
      // record self-describing even if a level ever exceeds it.
      capacities[level] = Math.Max(NodeExtensions.GetM(_options.M, level), Connections![level]?.Count ?? 0);
      size += 2 * sizeof(int) + capacities[level] * sizeof(int);
    }

    var buffer = new byte[size];
    var offset = 0;
    buffer[offset++] = SerializationVersion;
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), EntryPointer);
    offset += sizeof(int);
    buffer[offset++] = (byte)(IsDeleted ? 1 : 0);
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), levels);
    offset += sizeof(int);

    for (var level = 0; level < levels; level++)
    {
      var connections = Connections![level];
      var count = connections?.Count ?? 0;

      BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), capacities[level]);
      offset += sizeof(int);
      BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), count);
      offset += sizeof(int);

      for (var i = 0; i < count; i++)
      {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), connections![i]);
        offset += sizeof(int);
      }

      // Reserved slots stay zero: they fix the record size at its maximum so
      // later updates never outgrow it.
      offset += (capacities[level] - count) * sizeof(int);
    }

    return buffer;
  }

  public static AdjacencyPart Deserialize(ReadOnlyMemory<byte> data, SmallWorldOptions options)
  {
    var span = data.Span;
    var offset = 0;

    var version = span.ReadByte(ref offset);
    if (version != SerializationVersion)
      throw new InvalidDataException($"Unsupported AdjacencyPart serialization version: {version}");

    var entryPointer = span.ReadLEInt32(ref offset);
    var isDeleted = span.ReadByte(ref offset) == 1;
    var levels = span.ReadLEInt32(ref offset);
    if (levels < 0)
      throw new InvalidDataException("Invalid levels count in serialized AdjacencyPart payload.");

    var connections = new IList<int>[levels];
    for (var level = 0; level < levels; level++)
    {
      var capacity = span.ReadLEInt32(ref offset);
      var count = span.ReadLEInt32(ref offset);
      if (capacity < 0 || count < 0 || count > capacity ||
          offset + capacity * sizeof(int) > span.Length)
        throw new InvalidDataException("Invalid connection block in serialized AdjacencyPart payload.");

      var ids = new int[count];
      for (var i = 0; i < count; i++)
        ids[i] = span.ReadLEInt32(ref offset);

      // Skip the reserved (padded) slots.
      offset += (capacity - count) * sizeof(int);

      connections[level] = ids;
    }

    return new AdjacencyPart(options)
    {
      EntryPointer = entryPointer,
      IsDeleted = isDeleted,
      Connections = connections
    };
  }
}

/// <summary>
/// Disk-backed node list with the hnswlib-style runtime layout:
/// - one CANONICAL IndexNode per position, resident in RAM (id, levels,
///   adjacency, deletion flag) — indexing never allocates or deserializes;
/// - vectors are NOT held in RAM: nodes read them zero-copy from the memory
///   mapped {name}.vec (freshly inserted nodes keep a staging float[] until a
///   remap covers their offset, then it is dropped);
/// - mutations write through: adjacency records go to {name}.adj in place,
///   vector records are appended once to {name}.vec and never touched again.
/// Slot 0 is the entrypoint pointer (see AssignEntryPoint). The on-disk
/// format is unchanged from the previous split storage.
/// </summary>
internal sealed class SplitNodeList : IList<IndexNode>
{
  // Vectors staged in RAM before a remap makes them readable from the map.
  private const int StageLimit = 4096;

  private readonly StoredList<VectorPart, SmallWorldOptions> _vectors;
  private readonly StoredList<AdjacencyPart, SmallWorldOptions> _adjacency;
  private readonly MappedVectorFile _mapped;
  private readonly SmallWorldOptions _options;

  private readonly List<IndexNode> _nodes = new();
  private readonly List<IndexNode> _staged = new();

  public SplitNodeList(string vectorsPath, string adjacencyPath, SmallWorldOptions options, TimeSpan? flushInterval)
  {
    _options = options;
    _vectors = new StoredList<VectorPart, SmallWorldOptions>(
      new StoreListOptions { FilePath = vectorsPath, FlushInterval = flushInterval }, options);
    _adjacency = new StoredList<AdjacencyPart, SmallWorldOptions>(
      new StoreListOptions { FilePath = adjacencyPath, FlushInterval = flushInterval }, options);

    // A crash between the two appends of Add can leave one list longer than
    // the other: clamp to the shorter one so the shared index space stays
    // aligned (the dropped nodes are restored by the store reconciliation).
    while (_vectors.Count > _adjacency.Count) _vectors.RemoveAt(_vectors.Count - 1);
    while (_adjacency.Count > _vectors.Count) _adjacency.RemoveAt(_adjacency.Count - 1);

    _mapped = new MappedVectorFile(vectorsPath);
    LoadNodes();
  }

  // VectorPart record layout (see VectorPart.Serialize):
  // [version 1B][idlen 4B][id][maxLevel 4B][veclen 4B][floats]
  private static long FloatOffsetOf(long recordPosition, int idLength) =>
    recordPosition + 1 + sizeof(int) + idLength + 2 * sizeof(int);

  private void LoadNodes()
  {
    var count = _adjacency.Count;
    if (count == 0) return;

    _nodes.Capacity = count;

    for (var i = 0; i < count; i++)
    {
      var adjacency = _adjacency[i];
      var (position, length) = _vectors.GetItemLocation(i);

      // Only the record HEADER is read (id and vector location): the payload
      // stays untouched on disk until a distance needs it through the map.
      var version = _mapped.Bytes(position, 1)[0];
      if (version != VectorPart.FloatVersion && version != VectorPart.Sq8Version)
        throw new InvalidDataException($"Unsupported VectorPart serialization version: {version}");
      var quantized = version == VectorPart.Sq8Version;

      var idLength = BinaryPrimitives.ReadInt32LittleEndian(_mapped.Bytes(position + 1, sizeof(int)));
      if (idLength < 0 || 1 + 3 * sizeof(int) + idLength > length)
        throw new InvalidDataException("Invalid Id length in vector record header.");

      var id = _mapped.Bytes(position + 1 + sizeof(int), idLength).ToArray();
      var afterId = position + 1 + sizeof(int) + idLength;
      var maxLevel = BinaryPrimitives.ReadInt32LittleEndian(_mapped.Bytes(afterId, sizeof(int)));
      var dimensions = BinaryPrimitives.ReadInt32LittleEndian(_mapped.Bytes(afterId + sizeof(int), sizeof(int)));
      if (dimensions < 0 || 1 + 3 * sizeof(int) + idLength + (long)dimensions * (quantized ? 1 : sizeof(float)) > length)
        throw new InvalidDataException("Invalid vector length in vector record header.");

      _nodes.Add(new IndexNode(_options)
      {
        PositionId = i == 0 ? adjacency.EntryPointer : i,
        Id = new VectorKey { Value = id },
        MaxLevel = maxLevel,
        IsDeleted = adjacency.IsDeleted,
        Connections = adjacency.Connections,
        MappedVectors = _mapped,
        MappedFloatOffset = afterId + 2 * sizeof(int),
        MappedDimensions = dimensions,
        MappedQuantized = quantized
      });
    }
  }

  public int Count => _nodes.Count;
  public bool IsReadOnly => false;

  public void Add(IndexNode node)
  {
    if (node is null) return;

    // Reentrant under the graph lock (callers already hold lock(nodes)).
    lock (this)
    {
      var index = _nodes.Count;
      var idLength = node.Id.Value?.Length ?? 0;
      var quantized = node.RamQuantized;

      _vectors.Add(new VectorPart
      {
        Id = node.Id,
        Vector = quantized is null ? node.Vector : [],
        QuantizedVector = quantized,
        MaxLevel = node.MaxLevel
      });
      _adjacency.Add(new AdjacencyPart(_options)
      {
        EntryPointer = node.PositionId,
        IsDeleted = node.IsDeleted,
        Connections = node.Connections
      });

      var (position, _) = _vectors.GetItemLocation(index);
      node.MappedDimensions = quantized?.Length ?? node.VectorDimensions;
      node.MappedQuantized = quantized is not null;
      node.MappedFloatOffset = FloatOffsetOf(position, idLength);
      node.MappedVectors = _mapped;

      _nodes.Add(node);

      // The staging copies are dropped once a remap covers the new record.
      if (node.MappedDimensions > 0)
      {
        _staged.Add(node);
        if (_staged.Count >= StageLimit)
          ReleaseStagedVectors();
      }
    }
  }

  // Requires the lock.
  private void ReleaseStagedVectors()
  {
    _mapped.Remap();
    var mappedLength = _mapped.MappedLength;

    var kept = 0;
    for (var i = 0; i < _staged.Count; i++)
    {
      var node = _staged[i];
      var payloadBytes = (long)node.MappedDimensions * (node.MappedQuantized ? 1 : sizeof(float));
      if (node.MappedFloatOffset + payloadBytes <= mappedLength)
        node.ReleaseRamVector();
      else
        _staged[kept++] = node;
    }

    _staged.RemoveRange(kept, _staged.Count - kept);
  }

  public IndexNode this[int index]
  {
    get => _nodes[index];
    set
    {
      if (value is null) throw new ArgumentNullException(nameof(value));

      // Deliberately NOT lock(this): callers already serialize per node
      // (inserts/deletes hold the node lock, slot 0 the graph lock — which IS
      // this instance). Taking lock(this) here would invert the global
      // node → graph lock order and deadlock against allocations. The
      // adjacency StoredList has its own internal locking.
      if (index == 0)
      {
        // Slot 0 stores just the entrypoint pointer — the entrypoint's own
        // adjacency lives at its own index. Keep the canonical slot-0 view
        // aligned so reloads resolve nodes[nodes[0].PositionId].
        _nodes[0].PositionId = value.PositionId;
        _adjacency[0] = new AdjacencyPart(_options)
        {
          EntryPointer = value.PositionId,
          IsDeleted = false,
          Connections = Array.Empty<IList<int>>()
        };
        return;
      }

      // Canonical instances make this an identity write on the RAM side; the
      // real effect is the write-through of the adjacency record.
      _nodes[index] = value;
      _adjacency[index] = new AdjacencyPart(_options)
      {
        EntryPointer = value.PositionId,
        IsDeleted = value.IsDeleted,
        Connections = value.Connections
      };
    }
  }

  public void Flush()
  {
    lock (this)
      ReleaseStagedVectors();

    _vectors.Flush();
    _adjacency.Flush();
  }

  public async ValueTask DisposeAsync()
  {
    await _vectors.DisposeAsync();
    await _adjacency.DisposeAsync();
    _mapped.Dispose();
  }

  public void ShrinkDb()
  {
    // Deliberately a no-op: vectors are append-only and adjacency updates are
    // in-place, so there is no dead space to reclaim — and compacting the
    // vector file would move records and invalidate the mapped offsets.
  }

  public void Clear()
  {
    lock (this)
    {
      _vectors.Clear();
      _adjacency.Clear();
      _nodes.Clear();
      _staged.Clear();
    }
  }

  public IEnumerator<IndexNode> GetEnumerator()
  {
    var count = _nodes.Count;
    for (var i = 0; i < count; i++)
      yield return _nodes[i];
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  // Graph code only appends, indexes and enumerates: positional mutations
  // would break PositionId == index and are rejected loudly.
  public void Insert(int index, IndexNode item) => throw new NotSupportedException("Graph nodes are append-only.");
  public void RemoveAt(int index) => throw new NotSupportedException("Graph nodes are append-only.");
  public bool Remove(IndexNode item) => throw new NotSupportedException("Graph nodes are append-only.");
  public bool Contains(IndexNode item) => throw new NotSupportedException();
  public int IndexOf(IndexNode item) => throw new NotSupportedException();
  public void CopyTo(IndexNode[] array, int arrayIndex) => throw new NotSupportedException();
}
