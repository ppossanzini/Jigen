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
  // NoRecursion: no code path re-enters the lock (the upgradeable→write
  // transition in IndexOf is an upgrade, not a recursion) and it is cheaper.
  private readonly ReaderWriterLockSlim _itemsIndexLock = new(LockRecursionPolicy.NoRecursion);

  // Incremental flush bookkeeping (mutated under the write lock, consumed
  // under the upgradeable lock in Flush): entries [0.._flushedCount) are on
  // disk; in-place updates below the watermark are tracked individually.
  private int _flushedCount;
  private readonly HashSet<int> _dirtyIndexes = new();

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

  // Lazy hash → indices lookup for Contains/IndexOf/Remove: built on first
  // use (append-only workloads never pay for it), invalidated whenever indices
  // shift (Insert/RemoveAt/Remove). Hash matches are always confirmed by
  // comparing the stored bytes, so hash collisions cannot match a wrong item.
  private Dictionary<ulong, List<int>> _hashToIndices;

  // Requires the write lock.
  private void EnsureHashIndex()
  {
    if (_hashToIndices is not null) return;

    var map = new Dictionary<ulong, List<int>>(_itemsIndex.Count);
    for (int i = 0; i < _itemsIndex.Count; i++)
      AddToHashIndex(map, _itemsIndex[i].Hash, i);
    _hashToIndices = map;
  }

  private static void AddToHashIndex(Dictionary<ulong, List<int>> map, ulong hash, int index)
  {
    if (!map.TryGetValue(hash, out var indices))
      map[hash] = indices = new List<int>(1);
    indices.Add(index);
  }

  // Requires at least the read lock. Confirms a hash match against the bytes
  // actually stored on disk.
  private bool MatchesStoredBytes(int index, ReadOnlyMemory<byte> buffer)
  {
    var ii = _itemsIndex[index];
    if (ii.Length != buffer.Length) return false;

    var rented = ArrayPool<byte>.Shared.Rent(ii.Length);
    try
    {
      RandomAccess.Read(_data!.SafeFileHandle!, rented.AsSpan(0, ii.Length), ii.Position);
      return rented.AsSpan(0, ii.Length).SequenceEqual(buffer.Span);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(rented);
    }
  }

  // Requires at least the read lock and a built hash index.
  private int FindVerifiedIndex(ReadOnlyMemory<byte> buffer, ulong hash)
  {
    if (!_hashToIndices!.TryGetValue(hash, out var candidates)) return -1;

    foreach (var index in candidates)
      if (MatchesStoredBytes(index, buffer))
        return index;

    return -1;
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

    // Persist the corrected count on the next Flush. Everything loaded came
    // from disk: the flush watermark starts there.
    _header.Count = _itemsIndex.Count;
    _flushedCount = _itemsIndex.Count;
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

  private void WriteIndexRange(int from, int count)
  {
    if (count <= from) return;
    var span = CollectionsMarshal.AsSpan(_itemsIndex).Slice(from, count - from);
    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(span);
    RandomAccess.Write(_dataindex.SafeFileHandle!, bytes, (long)from * ItemIndex.Size);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void InvalidateCache()
  {
    Array.Clear(_readCache);
  }

  /// <summary>
  /// Absolute file position and length of the item at <paramref name="index"/>.
  /// Lets callers read the raw record through their own channel (e.g. a
  /// memory mapping) without deserializing it.
  /// </summary>
  public (long Position, int Length) GetItemLocation(int index)
  {
    _itemsIndexLock.EnterReadLock();
    try
    {
      var item = _itemsIndex[index];
      return (item.Position, item.Length);
    }
    finally
    {
      _itemsIndexLock.ExitReadLock();
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
      var hash = XxHash64.HashToUInt64(buffer.Span);
      _itemsIndex.Add(new ItemIndex()
      {
        Position = position,
        Length = buffer.Length,
        MaxLength = buffer.Length,
        Hash = hash
      });
      _header.Count++;

      // Appends do not shift indices: the hash index stays valid, just extend it.
      if (_hashToIndices is not null)
        AddToHashIndex(_hashToIndices, hash, newIndex);

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
      _hashToIndices = null;
      _flushedCount = 0;
      _dirtyIndexes.Clear();
    }
    finally
    {
      _itemsIndexLock.ExitWriteLock();
    }

    InitializeStore();
  }

  public bool Contains(T item)
  {
    return IndexOf(item) != -1;
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
    var buffer = item.Serialize();
    var hash = XxHash64.HashToUInt64(buffer.Span);

    _itemsIndexLock.EnterWriteLock();
    try
    {
      EnsureHashIndex();
      var index = FindVerifiedIndex(buffer, hash);
      if (index == -1) return false;

      // Removal shifts every following index: read cache and hash index
      // are both stale, and the index file needs a full rewrite.
      InvalidateCache();
      _hashToIndices = null;
      _itemsIndex.RemoveAt(index);
      _header.TombStonedCount++;
      _header.Count--;
      _flushedCount = 0;
      _dirtyIndexes.Clear();
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
    var buffer = item.Serialize();
    var hash = XxHash64.HashToUInt64(buffer.Span);

    // Upgradeable: concurrent with plain readers; upgrades to write only for
    // the one-off lazy build of the hash index.
    _itemsIndexLock.EnterUpgradeableReadLock();
    try
    {
      if (_hashToIndices is null)
      {
        _itemsIndexLock.EnterWriteLock();
        try
        {
          EnsureHashIndex();
        }
        finally
        {
          _itemsIndexLock.ExitWriteLock();
        }
      }

      return FindVerifiedIndex(buffer, hash);
    }
    finally
    {
      _itemsIndexLock.ExitUpgradeableReadLock();
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
      // Indices shift: read cache and hash index are both stale,
      // and the whole index needs rewriting on the next flush.
      InvalidateCache();
      _hashToIndices = null;
      _flushedCount = 0;
      _dirtyIndexes.Clear();
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
      _hashToIndices = null;
      _itemsIndex.RemoveAt(index);
      _header.TombStonedCount++;
      _header.Count--;
      _flushedCount = 0;
      _dirtyIndexes.Clear();
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

        var newHash = XxHash64.HashToUInt64(buffer.Span);
        _itemsIndex[index] = new ItemIndex()
        {
          Position = position,
          Length = buffer.Length,
          MaxLength = Math.Max(buffer.Length, ii.MaxLength),
          Hash = newHash
        };

        // No index shift, but this entry's hash changed: move it in the map.
        if (_hashToIndices is not null && ii.Hash != newHash)
        {
          if (_hashToIndices.TryGetValue(ii.Hash, out var oldIndices))
            oldIndices.Remove(index);
          AddToHashIndex(_hashToIndices, newHash, index);
        }

        // Updated below the flush watermark: only this entry needs rewriting.
        if (index < _flushedCount)
          _dirtyIndexes.Add(index);
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
