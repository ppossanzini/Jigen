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
  private const byte SerializationVersion = 1;

  public VectorKey Id { get; init; }
  public float[] Vector { get; init; } = [];
  public int MaxLevel { get; init; }

  public ReadOnlyMemory<byte> Serialize()
  {
    var id = Id.Value ?? [];
    var size = 1 + sizeof(int) + id.Length + sizeof(int) + sizeof(int) + Vector.Length * sizeof(float);
    var buffer = new byte[size];

    var offset = 0;
    buffer[offset++] = SerializationVersion;
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), id.Length);
    offset += sizeof(int);
    id.CopyTo(buffer.AsSpan(offset));
    offset += id.Length;
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), MaxLevel);
    offset += sizeof(int);
    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), Vector.Length);
    offset += sizeof(int);
    MemoryMarshal.AsBytes(Vector.AsSpan()).CopyTo(buffer.AsSpan(offset));

    return buffer;
  }

  public static VectorPart Deserialize(ReadOnlyMemory<byte> data, SmallWorldOptions options)
  {
    var span = data.Span;
    var offset = 0;

    var version = span.ReadByte(ref offset);
    if (version != SerializationVersion)
      throw new InvalidDataException($"Unsupported VectorPart serialization version: {version}");

    var idLength = span.ReadLEInt32(ref offset);
    if (idLength < 0 || offset + idLength > span.Length)
      throw new InvalidDataException("Invalid Id length in serialized VectorPart payload.");
    var id = span.Slice(offset, idLength).ToArray();
    offset += idLength;

    var maxLevel = span.ReadLEInt32(ref offset);

    var vectorLength = span.ReadLEInt32(ref offset);
    var vectorBytes = checked(vectorLength * sizeof(float));
    if (vectorLength < 0 || offset + vectorBytes > span.Length)
      throw new InvalidDataException("Invalid vector length in serialized VectorPart payload.");
    var vector = vectorLength == 0
      ? []
      : MemoryMarshal.Cast<byte, float>(span.Slice(offset, vectorBytes)).ToArray();

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
/// Disk-backed node list splitting each node across two StoredLists sharing
/// the same index space: immutable vectors ({name}.vec, append-only) and
/// mutable adjacency records ({name}.adj, fixed-size in-place updates).
/// Assigning <c>this[i] = node</c> persists ONLY the adjacency half.
/// Slot 0 is the entrypoint pointer: its adjacency record stores the
/// PositionId of the current entrypoint (see AssignEntryPoint).
/// </summary>
internal sealed class SplitNodeList : IList<IndexNode>
{
  private readonly int _cacheMask;

  private readonly StoredList<VectorPart, SmallWorldOptions> _vectors;
  private readonly StoredList<AdjacencyPart, SmallWorldOptions> _adjacency;
  private readonly SmallWorldOptions _options;

  private sealed class CacheEntry(int index, IndexNode value)
  {
    public readonly int Index = index;
    public readonly IndexNode Value = value;
  }

  private readonly CacheEntry[] _cache;

  public SplitNodeList(string vectorsPath, string adjacencyPath, SmallWorldOptions options, TimeSpan? flushInterval)
  {
    _options = options;

    // Every graph hop goes through this[i]: the composed-node cache is what
    // keeps a disk-backed search close to in-memory speed once warm.
    var slots = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(options.NodeCacheSize, 512));
    _cache = new CacheEntry[slots];
    _cacheMask = slots - 1;

    _vectors = new StoredList<VectorPart, SmallWorldOptions>(
      new StoreListOptions { FilePath = vectorsPath, FlushInterval = flushInterval }, options);
    _adjacency = new StoredList<AdjacencyPart, SmallWorldOptions>(
      new StoreListOptions { FilePath = adjacencyPath, FlushInterval = flushInterval }, options);

    // A crash between the two appends of Add can leave one list longer than
    // the other: clamp to the shorter one so the shared index space stays
    // aligned (the dropped nodes are restored by the store reconciliation).
    while (_vectors.Count > _adjacency.Count) _vectors.RemoveAt(_vectors.Count - 1);
    while (_adjacency.Count > _vectors.Count) _adjacency.RemoveAt(_adjacency.Count - 1);
  }

  public int Count => _adjacency.Count;
  public bool IsReadOnly => false;

  public void Add(IndexNode node)
  {
    if (node is null) return;

    var index = _adjacency.Count;
    _vectors.Add(new VectorPart { Id = node.Id, Vector = node.Vector, MaxLevel = node.MaxLevel });
    _adjacency.Add(new AdjacencyPart(_options)
    {
      EntryPointer = node.PositionId,
      IsDeleted = node.IsDeleted,
      Connections = node.Connections
    });

    if (index != 0)
      Volatile.Write(ref _cache[index & _cacheMask], new CacheEntry(index, node));
  }

  public IndexNode this[int index]
  {
    get
    {
      // Slot 0 is the entrypoint pointer and is read only on (re)load: it is
      // deliberately kept out of the hot cache.
      if (index != 0)
      {
        var cached = Volatile.Read(ref _cache[index & _cacheMask]);
        if (cached is not null && cached.Index == index)
          return cached.Value;
      }

      var vector = _vectors[index];
      var adjacency = _adjacency[index];

      var node = new IndexNode(_options)
      {
        PositionId = index == 0 ? adjacency.EntryPointer : index,
        Id = vector.Id,
        Vector = vector.Vector,
        MaxLevel = vector.MaxLevel,
        IsDeleted = adjacency.IsDeleted,
        Connections = adjacency.Connections
      };

      if (index != 0)
        Volatile.Write(ref _cache[index & _cacheMask], new CacheEntry(index, node));

      return node;
    }
    set
    {
      if (value is null) throw new ArgumentNullException(nameof(value));

      // Only the mutable half is persisted: the vector was written by Add and
      // never changes. Slot 0 stores just the entrypoint pointer — the
      // entrypoint's own adjacency lives at its own index.
      _adjacency[index] = new AdjacencyPart(_options)
      {
        EntryPointer = value.PositionId,
        IsDeleted = index != 0 && value.IsDeleted,
        Connections = index == 0 ? Array.Empty<IList<int>>() : value.Connections
      };

      if (index != 0)
        Volatile.Write(ref _cache[index & _cacheMask], new CacheEntry(index, value));
    }
  }

  public void Flush()
  {
    _vectors.Flush();
    _adjacency.Flush();
  }

  public async ValueTask DisposeAsync()
  {
    await _vectors.DisposeAsync();
    await _adjacency.DisposeAsync();
  }

  public void ShrinkDb()
  {
    // Vectors are append-only and adjacency updates are in-place, so the only
    // dead space comes from crash-recovery clamping: usually a no-op.
    _vectors.ShrinkDb();
    _adjacency.ShrinkDb();
  }

  public void Clear()
  {
    _vectors.Clear();
    _adjacency.Clear();
    Array.Clear(_cache);
  }

  public IEnumerator<IndexNode> GetEnumerator()
  {
    for (var i = 0; i < Count; i++)
      yield return this[i];
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
