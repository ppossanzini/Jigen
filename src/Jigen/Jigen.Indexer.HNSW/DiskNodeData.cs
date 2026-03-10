// <copyright>
// Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

using System.IO.MemoryMappedFiles;

namespace Jigen.Indexer;

/// <summary>
/// Disk-backed implementation of INodeDataStore using memory-mapped files.
/// bucketIndex is unused (always 0), position is the byte offset in the file.
/// </summary>
public sealed class DiskNodeData : INodeDataStore, IDisposable
{
  private readonly string _filePath;
  private MemoryMappedFile _mmf;
  private MemoryMappedViewAccessor _accessor;
  private int _currentPosition; // current write position in ints
  private long _capacityInInts;

  private const long DefaultCapacity = 4 * 1024 * 1024; // ~16 MB initially

  public DiskNodeData(string filePath, long initialCapacityInInts = DefaultCapacity)
  {
    _filePath = filePath;
    _capacityInInts = initialCapacityInInts;
    _currentPosition = 0;

    _mmf = MemoryMappedFile.CreateFromFile(
      _filePath, FileMode.Create, mapName: null,
      _capacityInInts * sizeof(int));
    _accessor = _mmf.CreateViewAccessor(0, _capacityInInts * sizeof(int));
  }

  public ReadOnlySpan<int> GetLayer(int bucketIndex, int position, int layerIndex, int maxLayer)
  {
    int layerStart = ReadInt(position + layerIndex) + maxLayer + 1;
    int layerEnd = ReadInt(position + layerIndex + 1) + maxLayer + 1;
    int count = layerEnd - layerStart;

    var result = new int[count];
    for (int i = 0; i < count; i++)
    {
      result[i] = ReadInt(position + layerStart + i);
    }

    return result;
  }

  public ReadOnlySpan<int> GetAll(int bucketIndex, int position, int maxLayers)
  {
    int layerEnd = ReadInt(position + maxLayers) + maxLayers + 1;
    var result = new int[layerEnd];
    for (int i = 0; i < layerEnd; i++)
    {
      result[i] = ReadInt(position + i);
    }

    return result;
  }

  public (int bucketIndex, int position, int maxLayers) Add(List<List<int>> list)
  {
    if (list.Count == 0) return (0, -1, 0);

    int totalSize = list.Sum(static v => v.Count);
    int maxLayer = list.Count;
    int finalLength = totalSize + maxLayer + 1;

    EnsureCapacity(_currentPosition + finalLength);

    int startPos = _currentPosition;

    // Write offset table
    int c = 0, j = 0;
    for (int i = 0; i < maxLayer; i++)
    {
      var l = list[i];
      WriteInt(startPos + i, c);
      foreach (var v in l)
      {
        WriteInt(startPos + maxLayer + 1 + j, v);
        j++;
      }

      c += l.Count;
    }

    WriteInt(startPos + maxLayer, c);

    _currentPosition += finalLength;
    return (0, startPos, maxLayer);
  }

  public (int bucketIndex, int position, int maxLayers) Add(ReadOnlySpan<int> data, int maxLayer)
  {
    EnsureCapacity(_currentPosition + data.Length);

    int startPos = _currentPosition;
    for (int i = 0; i < data.Length; i++)
    {
      WriteInt(startPos + i, data[i]);
    }

    _currentPosition += data.Length;
    return (0, startPos, maxLayer);
  }

  private int ReadInt(int positionInInts)
  {
    return _accessor.ReadInt32((long)positionInInts * sizeof(int));
  }

  private void WriteInt(int positionInInts, int value)
  {
    _accessor.Write((long)positionInInts * sizeof(int), value);
  }

  private void EnsureCapacity(long requiredInInts)
  {
    if (requiredInInts <= _capacityInInts) return;

    long newCapacity = Math.Max(_capacityInInts * 2, requiredInInts);
    _accessor.Dispose();
    _mmf.Dispose();

    _mmf = MemoryMappedFile.CreateFromFile(
      _filePath, FileMode.OpenOrCreate, mapName: null,
      newCapacity * sizeof(int));
    _accessor = _mmf.CreateViewAccessor(0, newCapacity * sizeof(int));
    _capacityInInts = newCapacity;
  }

  public void Dispose()
  {
    _accessor?.Dispose();
    _mmf?.Dispose();
  }
}