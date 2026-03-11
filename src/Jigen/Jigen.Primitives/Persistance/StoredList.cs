using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jigen.Persistance;

/// <summary>
/// Implementation of a list that stores items persistently to disk.
/// Performances are impacted by disk I/O operations and by serialization and deserialization methods implemented in IStorableItem.
/// </summary>
/// <typeparam name="T"></typeparam>
public class StoredList<T> : IList<T> where T : IStorableItem
{
  [StructLayout(LayoutKind.Explicit)]
  public struct StoredListHeader
  {
    [FieldOffset(0)] public int Count;

    [FieldOffset(4)] public int TombStonedCount;

    [FieldOffset(8)] public long NextItemPosition;

    [FieldOffset(16)] public long IndexPosition;

    // No need of initialization, this array overlaps struct memory to allow fast serialization
    [FieldOffset(0)] public byte[] HeaderData;
    public const int HeaderSize = 24;
  }

  [StructLayout(LayoutKind.Explicit)]
  public struct IndexItem
  {
    [FieldOffset(0)] public int Key;
    [FieldOffset(4)] public long Value;
    [FieldOffset(0)] public byte[] ItemData;
    public const int ItemDataSize = 12;
  }


  private StoredListHeader _header = new StoredListHeader();
  private StoreListOptions _options;
  private FileStream _data;
  private FileStream _dataindex;

// Index of items position on disk
  private readonly Dictionary<int, long> _itemsIndex = new();

  public StoredList(StoreListOptions options)
  {
    _options = options;
    _data = new FileStream(options.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
    _dataindex = new FileStream($"{options.FilePath}.index", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);

    InitializeStore();
    ReadIndex();
  }

  private void InitializeStore()
  {
    if (_data is null) throw new ArgumentNullException(nameof(_data));

    // This is a new file so i neeed to write fileHeader
    if (_data.Length == 0)
    {
      _header.Count = 0;
      _header.NextItemPosition = 0;
      _header.IndexPosition = StoredListHeader.HeaderSize;
      RandomAccess.Write(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
    }
    else
    {
      RandomAccess.Read(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
    }
  }

  private void ReadIndex()
  {
    Span<byte> buffer = stackalloc byte[IndexItem.ItemDataSize];
    for (var i = 0; i < _header.Count; i++)
    {
      RandomAccess.Read(_dataindex.SafeFileHandle!, buffer, i * IndexItem.ItemDataSize);
      IndexItem item = MemoryMarshal.Read<IndexItem>(buffer);
      this._itemsIndex.Add(item.Key, item.Value);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void WriteIndexAt(int position)
  {
    var idx = _itemsIndex.ElementAt(position);
    var itemIndex = new IndexItem() { Key = idx.Key, Value = idx.Value };
    RandomAccess.Write(_dataindex.SafeFileHandle!, itemIndex.ItemData.AsSpan(), position * IndexItem.ItemDataSize);
  }

  private void WriteIndex()
  {
    for (var i = 0; i < _header.Count; i++)
    {
      WriteIndexAt(i);
    }
  }

  public IEnumerator<T> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(T item)
  {
    throw new NotImplementedException();
  }

  public void Clear()
  {
    throw new NotImplementedException();
  }

  public bool Contains(T item)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(T[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(T item)
  {
    throw new NotImplementedException();
  }

  public int Count { get; }
  public bool IsReadOnly { get; }

  public int IndexOf(T item)
  {
    throw new NotImplementedException();
  }

  public void Insert(int index, T item)
  {
    throw new NotImplementedException();
  }

  public void RemoveAt(int index)
  {
    throw new NotImplementedException();
  }

  public T this[int index]
  {
    get => throw new NotImplementedException();
    set => throw new NotImplementedException();
  }
}