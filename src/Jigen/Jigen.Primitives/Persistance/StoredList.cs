using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jigen.DataStructures;
using System.IO.Hashing;

namespace Jigen.Persistance;

/// <summary>
/// Implementation of a list that stores items persistently to disk.
/// Optimized with: batch I/O, direct-mapped read cache, unsafe struct writes,
/// CollectionsMarshal batch index flush.
/// </summary>
public partial class StoredList<T, TOptions> : IList<T> where T : IStorableItem<T, TOptions>
{
  private StoredListHeader _header = new StoredListHeader();
  private StoreListOptions _options;
  private readonly TOptions _itemOptions;
  private FileStream _data;
  private FileStream _dataindex;
  private readonly List<ItemIndex> _itemsIndex = new();
  private readonly ReaderWriterLockSlim _itemsIndexLock = new(LockRecursionPolicy.SupportsRecursion);

  // Direct-mapped read cache: avoids repeated file I/O + deserialization
  // for the same index (critical for HNSW graph traversal).
  private const int ReadCacheSlots = 512;
  private const int ReadCacheMask = ReadCacheSlots - 1;
  private readonly T[] _cacheValues = new T[ReadCacheSlots];
  private readonly int[] _cacheKeys = new int[ReadCacheSlots];

  public StoredList(StoreListOptions options, TOptions itemOptions)
  {
    _options = options;
    _itemOptions = itemOptions;
    _data = new FileStream(options.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
    _dataindex = new FileStream($"{options.FilePath}.index", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);

    Array.Fill(_cacheKeys, -1);

    InitializeStore();
    ReadIndex();

    _flushTimer = new PeriodicTimer(options.FlushInterval ?? TimeSpan.FromSeconds(30));
    _flushTask = Task.Run(() => FlushLoopAsync(_cts.Token));
  }

  private unsafe void InitializeStore()
  {
    if (_data is null) throw new ArgumentNullException(nameof(_data));

    if (_data.Length == 0)
    {
      _header.Count = 0;
      _header.TombStonedCount = 0;
      _header.NextItemPosition = StoredListHeader.Size;
      // Write header without allocation: unsafe pointer → ReadOnlySpan
      fixed (StoredListHeader* p = &_header)
        RandomAccess.Write(_data.SafeFileHandle!, new ReadOnlySpan<byte>(p, StoredListHeader.Size), 0);
    }
    else
    {
      // stackalloc instead of heap-allocated byte[]
      Span<byte> buffer = stackalloc byte[StoredListHeader.Size];
      RandomAccess.Read(_data.SafeFileHandle!, buffer, 0);
      _header = MemoryMarshal.Cast<byte, StoredListHeader>(buffer)[0];
    }
  }

  private void ReadIndex()
  {
    if (_header.Count <= 0) return;

    int count = _header.Count;
    int totalBytes = count * ItemIndex.Size;

    // Batch read: single I/O call instead of N individual reads
    var rentedBuffer = ArrayPool<byte>.Shared.Rent(totalBytes);
    try
    {
      RandomAccess.Read(_dataindex.SafeFileHandle!, rentedBuffer.AsSpan(0, totalBytes), 0);
      var indices = MemoryMarshal.Cast<byte, ItemIndex>(rentedBuffer.AsSpan(0, totalBytes));
      _itemsIndex.EnsureCapacity(count);
      for (int i = 0; i < count; i++)
        _itemsIndex.Add(indices[i]);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(rentedBuffer);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private unsafe void WriteIndexAt(int position)
  {
    var itemIndex = _itemsIndex[position];
    RandomAccess.Write(_dataindex.SafeFileHandle!,
      new ReadOnlySpan<byte>(&itemIndex, ItemIndex.Size),
      (long)position * ItemIndex.Size);
  }

  private void WriteIndex()
  {
    if (_itemsIndex.Count == 0) return;
    // Batch write: single I/O call via CollectionsMarshal direct span access
    var span = CollectionsMarshal.AsSpan(_itemsIndex);
    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(span);
    RandomAccess.Write(_dataindex.SafeFileHandle!, bytes, 0);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void InvalidateCache()
  {
    Array.Fill(_cacheKeys, -1);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void InvalidateCacheSlot(int index)
  {
    _cacheKeys[index & ReadCacheMask] = -1;
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
      int newIndex = _itemsIndex.Count;
      _itemsIndex.Add(new ItemIndex()
      {
        Position = position,
        Length = buffer.Length,
        MaxLength = buffer.Length,
        Hash = XxHash64.HashToUInt64(buffer.Span)
      });
      Interlocked.Increment(ref _header.Count);

      // Populate cache for the newly added item
      int slot = newIndex & ReadCacheMask;
      _cacheValues[slot] = item;
      _cacheKeys[slot] = newIndex;
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
      InvalidateCache();
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

      InvalidateCacheSlot(index);
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
      // Invalidate all cache after insert (indices shift)
      InvalidateCache();
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
      InvalidateCache();
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
      // Direct-mapped cache check (lock-free read path)
      int slot = index & ReadCacheMask;
      if (Volatile.Read(ref _cacheKeys[slot]) == index)
      {
        T cached = _cacheValues[slot];
        if (cached is not null) return cached;
      }

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
        var result = T.Deserialize(buffer.AsMemory(0, ii.Length), _itemOptions);

        // Populate cache
        _cacheValues[slot] = result;
        Volatile.Write(ref _cacheKeys[slot], index);

        return result;
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

      // Update cache with the new value
      int slot = index & ReadCacheMask;
      _cacheValues[slot] = value;
      Volatile.Write(ref _cacheKeys[slot], index);
    }
  }
}
