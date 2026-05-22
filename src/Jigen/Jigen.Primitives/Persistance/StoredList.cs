using System.Buffers;
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
public partial class StoredList<T, TOptions> : IList<T> where T : IStorableItem<T, TOptions>
{
  private StoredListHeader _header = new StoredListHeader();
  private StoreListOptions _options;
  private readonly TOptions _itemOptions;
  private FileStream _data;
  private FileStream _dataindex;
  private readonly List<ItemIndex> _itemsIndex = new();
  private readonly ReaderWriterLockSlim _itemsIndexLock = new(LockRecursionPolicy.SupportsRecursion);

  public StoredList(StoreListOptions options, TOptions itemOptions)
  {
    _options = options;
    _itemOptions = itemOptions;
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

    if (_data.Length == 0)
    {
      _header.Count = 0;
      _header.TombStonedCount = 0;
      _header.NextItemPosition = Marshal.SizeOf<StoredListHeader>();
      RandomAccess.Write(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
    }
    else
    {
      Span<byte> buffer = new byte[Marshal.SizeOf<StoredListHeader>()];
      RandomAccess.Read(_data.SafeFileHandle!, buffer, 0);
      _header = MemoryMarshal.Cast<byte, StoredListHeader>(buffer)[0];
    }
  }

  private void ReadIndex()
  {
    Span<byte> buffer = new byte[Marshal.SizeOf<ItemIndex>()];
    for (var i = 0; i < _header.Count; i++)
    {
      RandomAccess.Read(_dataindex.SafeFileHandle!, buffer, i * Marshal.SizeOf<ItemIndex>());
      this._itemsIndex.Add(MemoryMarshal.Cast<byte, ItemIndex>(buffer)[0]);
      ;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void WriteIndexAt(int position)
  {
    var itemIndex = _itemsIndex.ElementAt(position);
    RandomAccess.Write(_dataindex.SafeFileHandle!, itemIndex.Data.AsSpan(), position * Marshal.SizeOf<ItemIndex>());
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
    int initialCount;
    
    _itemsIndexLock.EnterReadLock();
    try
    { initialCount = _itemsIndex.Count; }
    finally
    {
      _itemsIndexLock.ExitReadLock();
    }

    for (int i = 0; i < initialCount; i++)
    {
      ItemIndex ii;
      _itemsIndexLock.EnterReadLock();
      try
      {
        // Fallback di sicurezza nel caso subisca uno shrink o ritiri durante l'enumerazione
        if (i >= _itemsIndex.Count) break;
        ii = _itemsIndex[i];
      }
      finally
      {
        _itemsIndexLock.ExitReadLock();
      }

      var buffer = ArrayPool<byte>.Shared.Rent(ii.Length);
      try
      {
        RandomAccess.Read(_data!.SafeFileHandle!, buffer.AsSpan(0, ii.Length), ii.Position);
        yield return T.Deserialize(buffer.AsMemory(0, ii.Length), _itemOptions);
      }
      finally
      {
        ArrayPool<byte>.Shared.Return(buffer);
      }
    }
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
    RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);
    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.Add(new ItemIndex()
      {
        Position = position,
        Length = buffer.Length,
        MaxLength = buffer.Length,
        Hash = XxHash64.HashToUInt64(buffer.Span)
      });
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
    var hash = XxHash64.HashToUInt64(item.Serialize().Span);
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
    if (array is null) throw new ArgumentNullException(nameof(array));
    if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "arrayIndex must be >= 0");
    if (arrayIndex + Count > array.Length)
      throw new ArgumentException(
        "The number of elements in the source ICollection is greater than the available space from arrayIndex to the end of the destination array.");

    _itemsIndexLock.EnterReadLock();
    try
    {
      for (int i = 0; i < _itemsIndex.Count; i++)
      {
        array[arrayIndex + i] = this[i];
      }
    }
    finally
    {
      _itemsIndexLock.ExitReadLock();
    }
  }

  public bool Remove(T item)
  {
    if (item is null) return false;
    var hash = XxHash64.HashToUInt64(item.Serialize().Span);

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
    var hash = XxHash64.HashToUInt64(item.Serialize().Span);
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
    RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);

    _itemsIndexLock.EnterWriteLock();
    try
    {
      _itemsIndex.Insert(index, new ItemIndex()
      {
        Position = position,
        MaxLength = buffer.Length,
        Length = buffer.Length,
        Hash = XxHash64.HashToUInt64(buffer.Span)
      });
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
      ItemIndex ii;
      _itemsIndexLock.EnterReadLock();
      try
      {
        ii = _itemsIndex[index];
      }
      finally
      {
        _itemsIndexLock.ExitReadLock();
      }

      var buffer = ArrayPool<byte>.Shared.Rent(ii.Length);
      try
      {
        RandomAccess.Read(_data!.SafeFileHandle!, buffer.AsSpan(0, ii.Length), ii.Position);
        return T.Deserialize(buffer.AsMemory(0, ii.Length), _itemOptions);
      }
      finally
      {
        ArrayPool<byte>.Shared.Return(buffer);
      }
    }
    set
    {
      if (value is null) throw new ArgumentNullException(nameof(value), "Value cannot be null");

      var ii = _itemsIndex[index];
      var buffer = value.Serialize();

      long position = ii.Position;
      if (buffer.Length > ii.MaxLength)
      {
        position = Interlocked.Add(ref _header.NextItemPosition, buffer.Length) - buffer.Length;
        Interlocked.Increment(ref _header.TombStonedCount);
      }

      RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);

      _itemsIndexLock.EnterWriteLock();
      try
      {
        _itemsIndex[index] = new ItemIndex()
        {
          Position = position,
          Length = buffer.Length,
          MaxLength = Math.Max(buffer.Length, ii.MaxLength),
          Hash = XxHash64.HashToUInt64(buffer.Span)
        };
      }
      finally
      {
        _itemsIndexLock.ExitWriteLock();
      }
    }
  }
}