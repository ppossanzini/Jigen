using System.Collections;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jigen.DataStructures;
using System.IO.Hashing;

namespace Jigen.Persistance;

/// <summary>
/// Implementation of a list that stores items persistently to disk.
/// Performances are impacted by disk I/O operations and by serialization and deserialization methods implemented in IStorableItem.
/// </summary>
/// <typeparam name="T"></typeparam>
public partial class StoredList<T> : IList<T> where T : IStorableItem<T>
{
  private StoredListHeader _header = new StoredListHeader();
  private StoreListOptions _options;
  private FileStream _data;
  private FileStream _dataindex;
  private readonly List<ItemIndex> _itemsIndex = new();
  private readonly ReaderWriterLockSlim _itemsIndexLock = new();

  public StoredList(StoreListOptions options)
  {
    _options = options;
    _data = new FileStream(options.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
    _dataindex = new FileStream($"{options.FilePath}.index", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);

    InitializeStore();
    ReadIndex();

    _flushTimer = new PeriodicTimer(options.FlushInterval ?? TimeSpan.FromSeconds(30));
    _flushTask = Task.Run(() => FlushLoopAsync(_cts.Token));
  }

  private void InitializeStore()
  {
    if (_data is null) throw new ArgumentNullException(nameof(_data));

    // This is a new file so i neeed to write fileHeader
    if (_data.Length == 0)
    {
      _header.Count = 0;
      _header.TombStonedCount = 0;
      _header.NextItemPosition = StoredListHeader.HeaderSize;
      RandomAccess.Write(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
    }
    else
    {
      RandomAccess.Read(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
    }
  }

  private void ReadIndex()
  {
    Span<byte> buffer = stackalloc byte[ItemIndex.ItemIndexSize];
    for (var i = 0; i < _header.Count; i++)
    {
      RandomAccess.Read(_dataindex.SafeFileHandle!, buffer, i * ItemIndex.ItemIndexSize);
      this._itemsIndex.Add(new ItemIndex() { Data = buffer.ToArray() });
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void WriteIndexAt(int position)
  {
    var itemIndex = _itemsIndex.ElementAt(position);
    RandomAccess.Write(_dataindex.SafeFileHandle!, itemIndex.Data.AsSpan(), position * ItemIndex.ItemIndexSize);
  }

  private void WriteIndex()
  {
    for (var i = 0; i < _itemsIndex.Count; i++)
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
    if (item is null) return;
    var buffer = item.Serialize();

    var position = Interlocked.Add(ref _header.NextItemPosition, buffer.Length) - buffer.Length;
    RandomAccess.Write(_data!.SafeFileHandle!, buffer, position);
    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.Add(new ItemIndex() { Position = position, Length = buffer.Length, Hash = XxHash64.HashToUInt64(buffer) });
      Interlocked.Increment(ref _header.Count);
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }
  }

  public void Clear()
  {
    this._data.SetLength(0);
    this._dataindex.SetLength(0);

    _data.Flush(true);
    _dataindex.Flush(true);
    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.Clear();
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }

    InitializeStore();
  }

  public bool Contains(T item)
  {
    var hash = XxHash64.HashToUInt64(item.Serialize());
    _itemsIndexLock.EnterReadLock();
    try
    {
      var index = _itemsIndex.FindIndex(i => i.Hash == hash);
      return index != -1;
    }
    finally
    {
      _itemsIndexLock.ExitReadLock();
    }
  }

  public void CopyTo(T[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(T item)
  {
    if (item is null) return false;
    var hash = XxHash64.HashToUInt64(item.Serialize());

    _itemsIndexLock.EnterWriteLock();
    try
    {
      var index = _itemsIndex.FindIndex(i => i.Hash == hash);
      if (index == -1) return false;

      _itemsIndex.RemoveAt(index);
      Interlocked.Increment(ref _header.TombStonedCount);
      Interlocked.Decrement(ref _header.Count);
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }

    return true;
  }

  public int Count => _itemsIndex.Count;
  public bool IsReadOnly => false;

  public int IndexOf(T item)
  {
    if (item is null) return -1;
    var hash = XxHash64.HashToUInt64(item.Serialize());
    _itemsIndexLock.EnterReadLock();
    try
    {
      return _itemsIndex.FindIndex(i => i.Hash == hash);
    }
    finally
    {
      _itemsIndexLock.ExitReadLock();
    }
  }

  public void Insert(int index, T item)
  {
    if (item is null) return;
    if (index < 0 || index > _itemsIndex.Count) throw new ArgumentOutOfRangeException(nameof(index));
    var buffer = item.Serialize();
    var position = Interlocked.Add(ref _header.NextItemPosition, buffer.Length) - buffer.Length;
    RandomAccess.Write(_data!.SafeFileHandle!, buffer, position);

    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.Insert(index, new ItemIndex() { Position = position, Length = buffer.Length, Hash = XxHash64.HashToUInt64(buffer) });
      Interlocked.Increment(ref _header.Count);
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }
  }

  public void RemoveAt(int index)
  {
    if (index < 0 || index >= _itemsIndex.Count) throw new ArgumentOutOfRangeException(nameof(index));
    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.RemoveAt(index);
      Interlocked.Increment(ref _header.TombStonedCount);
      Interlocked.Decrement(ref _header.Count);
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }
  }

  public T this[int index]
  {
    get
    {
      _itemsIndexLock.EnterReadLock();
      try
      {
        var ii = _itemsIndex[index];
        var buffer = new byte[ii.Length];
        RandomAccess.Read(_data!.SafeFileHandle!, buffer, ii.Position);
        return T.Deserialize(buffer);
      }
      finally
      {
        _itemsIndexLock.ExitReadLock();
      }
    }
    set
    {
      if (value is null) throw new ArgumentNullException(nameof(value), "Value cannot be null");

      var buffer = value.Serialize();

      var position = Interlocked.Add(ref _header.NextItemPosition, buffer.Length) - buffer.Length;
      RandomAccess.Write(_data!.SafeFileHandle!, buffer, position);

      _itemsIndexLock.EnterWriteLock();
      try
      {
        _itemsIndex[index] = new ItemIndex() { Position = position, Length = buffer.Length, Hash = XxHash64.HashToUInt64(buffer) };
      }
      finally
      {
        _itemsIndexLock.ExitWriteLock();
      }
    }
  }
}