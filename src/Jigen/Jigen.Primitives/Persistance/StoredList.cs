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
  // Each slot holds an immutable (index, value) pair swapped with a single
  // reference write, so readers can never observe a key paired with another
  // entry's value.
  private const int ReadCacheSlots = 512;
  private const int ReadCacheMask = ReadCacheSlots - 1;
  private readonly ReadCacheEntry[] _readCache = new ReadCacheEntry[ReadCacheSlots];

  private sealed class ReadCacheEntry(int index, T value)
  {
    public readonly int Index = index;
    public readonly T Value = value;
  }

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
      _header.NextItemPosition = StoredListHeader.Size;
      WriteHeader();
    }
    else
    {
      // stackalloc instead of heap-allocated byte[]
      Span<byte> buffer = stackalloc byte[StoredListHeader.Size];
      RandomAccess.Read(_data.SafeFileHandle!, buffer, 0);
      _header = MemoryMarshal.Cast<byte, StoredListHeader>(buffer)[0];
    }
  }

  // Write header without allocation: unsafe pointer → ReadOnlySpan
  internal unsafe void WriteHeader()
  {
    fixed (StoredListHeader* p = &_header)
      RandomAccess.Write(_data.SafeFileHandle!, new ReadOnlySpan<byte>(p, StoredListHeader.Size), 0);
  }

  private void ReadIndex()
  {
    if (_header.Count <= 0) return;

    int count = _header.Count;

    // A crash between the data write and the index flush can leave the header
    // claiming more entries than the index file actually holds: clamp to what exists.
    long storedEntries = _dataindex.Length / ItemIndex.Size;
    if (count > storedEntries) count = (int)storedEntries;

    if (count <= 0)
    {
      _header.Count = 0;
      return;
    }

    int totalBytes = count * ItemIndex.Size;

    // Batch read: single I/O call instead of N individual reads
    var rentedBuffer = ArrayPool<byte>.Shared.Rent(totalBytes);
    try
    {
      RandomAccess.Read(_dataindex.SafeFileHandle!, rentedBuffer.AsSpan(0, totalBytes), 0);
      var indices = MemoryMarshal.Cast<byte, ItemIndex>(rentedBuffer.AsSpan(0, totalBytes));
      long dataLength = _data.Length;
      _itemsIndex.EnsureCapacity(count);
      for (int i = 0; i < count; i++)
      {
        var entry = indices[i];
        // Entries past a torn write reference data that never reached the file
        // (zeroed/garbage records fail the Position check): stop at the first invalid one.
        if (entry.Position < StoredListHeader.Size ||
            entry.Length < 0 ||
            entry.MaxLength < entry.Length ||
            entry.Position > dataLength - entry.MaxLength)
          break;

        _itemsIndex.Add(entry);
      }
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(rentedBuffer);
    }

    // Persist the corrected count on the next Flush.
    _header.Count = _itemsIndex.Count;
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
    Array.Clear(_readCache);
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
      byte[] buffer;
      int length;

      _itemsIndexLock.EnterReadLock();
      try
      {
        if (i >= _itemsIndex.Count) break;
        var ii = _itemsIndex[i];

        length = ii.Length;
        buffer = ArrayPool<byte>.Shared.Rent(length);
        // File read inside the lock: ShrinkDb can move records concurrently.
        RandomAccess.Read(_data!.SafeFileHandle!, buffer.AsSpan(0, length), ii.Position);
      }
      finally
      {
        _itemsIndexLock.ExitReadLock();
      }

      try
      {
        yield return T.Deserialize(buffer.AsMemory(0, length), _itemOptions);
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

    // Position reservation and file write must happen under the write lock:
    // ShrinkDb moves records and truncates the file while holding it, so an
    // unprotected write could land past the truncation point and be lost.
    _itemsIndexLock.EnterWriteLock();
    try
    {
      var position = _header.NextItemPosition;
      _header.NextItemPosition += buffer.Length;
      RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);

      int newIndex = _itemsIndex.Count;
      _itemsIndex.Add(new ItemIndex()
      {
        Position = position,
        Length = buffer.Length,
        MaxLength = buffer.Length,
        Hash = XxHash64.HashToUInt64(buffer.Span)
      });
      _header.Count++;

      // Populate cache for the newly added item
      Volatile.Write(ref _readCache[newIndex & ReadCacheMask], new ReadCacheEntry(newIndex, item));
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

      // Removal shifts every following index: the whole cache is stale.
      InvalidateCache();
      _itemsIndex.RemoveAt(index);
      _header.TombStonedCount++;
      _header.Count--;
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

    _itemsIndexLock.EnterWriteLock();
    try
    {
      var position = _header.NextItemPosition;
      _header.NextItemPosition += buffer.Length;
      RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);

      _itemsIndex.Insert(index, new ItemIndex()
      {
        Position = position,
        MaxLength = buffer.Length,
        Length = buffer.Length,
        Hash = XxHash64.HashToUInt64(buffer.Span)
      });
      _header.Count++;
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
      _header.TombStonedCount++;
      _header.Count--;
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
      var cached = Volatile.Read(ref _readCache[slot]);
      if (cached is not null && cached.Index == index)
        return cached.Value;

      T result;
      _itemsIndexLock.EnterReadLock();
      try
      {
        var ii = _itemsIndex[index];
        var buffer = ArrayPool<byte>.Shared.Rent(ii.Length);
        try
        {
          // The file read must stay inside the lock: ShrinkDb moves records
          // and truncates the file while holding the write lock.
          RandomAccess.Read(_data!.SafeFileHandle!, buffer.AsSpan(0, ii.Length), ii.Position);
          result = T.Deserialize(buffer.AsMemory(0, ii.Length), _itemOptions);
        }
        finally
        {
          ArrayPool<byte>.Shared.Return(buffer);
        }
      }
      finally
      {
        _itemsIndexLock.ExitReadLock();
      }

      // Populate cache
      Volatile.Write(ref _readCache[slot], new ReadCacheEntry(index, result));
      return result;
    }
    set
    {
      if (value is null) throw new ArgumentNullException(nameof(value), "Value cannot be null");

      var buffer = value.Serialize();

      // The whole operation runs under the write lock: reading the ItemIndex,
      // deciding whether to reuse the slot and writing the file must be atomic
      // w.r.t. ShrinkDb, which reassigns positions and truncates the file.
      _itemsIndexLock.EnterWriteLock();
      try
      {
        var ii = _itemsIndex[index];

        long position = ii.Position;
        if (buffer.Length > ii.MaxLength)
        {
          position = _header.NextItemPosition;
          _header.NextItemPosition += buffer.Length;
          _header.TombStonedCount++;
        }

        RandomAccess.Write(_data!.SafeFileHandle!, buffer.Span, position);

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
      Volatile.Write(ref _readCache[index & ReadCacheMask], new ReadCacheEntry(index, value));
    }
  }
}
