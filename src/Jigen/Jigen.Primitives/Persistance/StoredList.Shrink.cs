using System.Buffers;

namespace Jigen.Persistance;

public partial class StoredList<T, TOptions> : IList<T> where T : IStorableItem<T, TOptions>
{
  public void ShrinkDb()
  {
    this._itemsIndexLock.EnterWriteLock();
    try
    {
      int count = _itemsIndex.Count;
      if (count == 0)
      {
        _header.Count = 0;
        _header.TombStonedCount = 0;
        _header.NextItemPosition = StoredListHeader.Size;
        _data.SetLength(StoredListHeader.Size);
        _dataindex.SetLength(0);
        WriteHeader();
        _flushedCount = 0;
        _dirtyIndexes.Clear();
        return;
      }

      // Build (originalIndex, item) pairs sorted by position → O(N log N) total
      // instead of _itemsIndex.IndexOf(item) per item → O(N²)
      var indexed = new (int originalIndex, ItemIndex item)[count];
      int maxRequiredSize = 0;
      for (int i = 0; i < count; i++)
      {
        var item = _itemsIndex[i];
        indexed[i] = (i, item);
        if (item.MaxLength > maxRequiredSize)
          maxRequiredSize = item.MaxLength;
      }

      Array.Sort(indexed, (a, b) => a.item.Position.CompareTo(b.item.Position));

      long position = StoredListHeader.Size;
      var sharedBuffer = maxRequiredSize > 0 ? ArrayPool<byte>.Shared.Rent(maxRequiredSize) : Array.Empty<byte>();

      try
      {
        foreach (var (originalIndex, item) in indexed)
        {
          var bufferSlice = sharedBuffer.AsSpan(0, item.MaxLength);

          RandomAccess.Read(_data.SafeFileHandle, bufferSlice, item.Position);
          RandomAccess.Write(_data.SafeFileHandle, bufferSlice, position);

          _itemsIndex[originalIndex] = new ItemIndex()
          {
            Position = position,
            Length = item.Length,
            MaxLength = item.MaxLength,
            Hash = item.Hash
          };

          position += item.MaxLength;
        }
      }
      finally
      {
        if (maxRequiredSize > 0)
          ArrayPool<byte>.Shared.Return(sharedBuffer);
      }

      this._header.Count = count;
      this._header.TombStonedCount = 0;
      this._header.NextItemPosition = position;
      _data.SetLength(position);

      // Invalidate read cache after compaction (positions changed)
      InvalidateCache();

      // Index and header must be rewritten while still holding the write lock:
      // a concurrent Add could otherwise mutate _itemsIndex while WriteIndex
      // reads its span. Truncate the index file too: removals leave stale
      // trailing records beyond the live count.
      WriteIndex();
      _dataindex.SetLength((long)count * ItemIndex.Size);
      WriteHeader();

      // Everything is on disk: reset the incremental-flush bookkeeping.
      _flushedCount = count;
      _dirtyIndexes.Clear();
    }
    finally
    {
      if (this._itemsIndexLock.IsWriteLockHeld)
        this._itemsIndexLock.ExitWriteLock();
    }

    _data.Flush(true);
    _dataindex.Flush(true);
  }
}
