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

      foreach (var item in items)
      {
        var buffer = ArrayPool<byte>.Shared.Rent(item.MaxLength);

        RandomAccess.Read(_data.SafeFileHandle, buffer, item.Position);
        RandomAccess.Write(_data.SafeFileHandle, buffer, position);

        var idx = _itemsIndex.IndexOf(item);
        _itemsIndex[idx] = new ItemIndex()
        {
          Position = position,
          Length = item.Length,
          MaxLength = item.MaxLength,
          Hash = item.Hash
        };
        ArrayPool<byte>.Shared.Return(buffer);

        position = position + item.MaxLength;
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