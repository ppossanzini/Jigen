using System.Buffers;
using System.Runtime.InteropServices;

namespace Jigen.Persistance;

public partial class StoredList<T, TOptions> : IList<T> where T : IStorableItem<T, TOptions>
{
  public void ShrinkDB()
  {
    this._itemsIndexLock.EnterWriteLock();
    try
    {
      var items = this._itemsIndex.OrderBy(i => i.Position).ToArray();

      var position = Marshal.SizeOf<StoredListHeader>();

      // Search for the maximum required buffer size to read items in chunks
      var maxRequiredSize = items.Length > 0 ? items.Max(i => i.MaxLength) : 0;
      var sharedBuffer = maxRequiredSize > 0 ? ArrayPool<byte>.Shared.Rent(maxRequiredSize) : Array.Empty<byte>();

      try
      {
        foreach (var item in items)
        {
          // Slice the shared buffer to the required size for the current item
          // This allows us to reuse the same buffer for multiple items without allocating a new one each time
          // Read and Write operations are performed using the sliced buffer, ensuring efficient memory usage while processing items in chunks
          var bufferSlice = sharedBuffer.AsSpan(0, item.MaxLength);

          RandomAccess.Read(_data.SafeFileHandle, bufferSlice, item.Position);
          RandomAccess.Write(_data.SafeFileHandle, bufferSlice, position);

          var idx = _itemsIndex.IndexOf(item);
          _itemsIndex[idx] = new ItemIndex()
          {
            Position = position,
            Length = item.Length,
            MaxLength = item.MaxLength,
            Hash = item.Hash
          };

          position = position + item.MaxLength;
        }
      }
      finally
      {
        if (maxRequiredSize > 0)
          ArrayPool<byte>.Shared.Return(sharedBuffer);
      }

      this._header.Count = _itemsIndex.Count;
      this._header.TombStonedCount = 0;
      this._header.NextItemPosition = position;
      _data.SetLength(position);
    }
    finally
    {
      if (this._itemsIndexLock.IsWriteLockHeld)
        this._itemsIndexLock.ExitWriteLock();
    }

    _data.Flush(true);
    WriteIndex();
    _dataindex.Flush(true);
  }
}