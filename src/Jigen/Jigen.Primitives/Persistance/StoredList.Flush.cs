using System.Runtime.InteropServices;

namespace Jigen.Persistance;

public partial class StoredList<T, TOptions> : IList<T> where T : IStorableItem<T, TOptions>
{
  private readonly PeriodicTimer _flushTimer;
  private readonly CancellationTokenSource _cts = new();
  private readonly Task _flushTask;

  private async Task FlushLoopAsync(CancellationToken ct)
  {
    try
    {
      while (await _flushTimer.WaitForNextTickAsync(ct))
      {
        Flush();
      }
    }
    catch (OperationCanceledException)
    {
    }
  }

  public unsafe void Flush()
  {
    _itemsIndexLock.EnterReadLock();
    try
    {
      // WriteIndex() uses CollectionsMarshal.AsSpan → single I/O for all indices
      WriteIndex();

      // Write header without byte[] allocation: unsafe pointer → ReadOnlySpan
      fixed (StoredListHeader* p = &_header)
        RandomAccess.Write(_data.SafeFileHandle!, new ReadOnlySpan<byte>(p, StoredListHeader.Size), 0);
    }
    finally
    {
      _itemsIndexLock.ExitReadLock();
    }
    _data.Flush(flushToDisk: true);
    _dataindex.Flush(flushToDisk: true);
  }

  public async ValueTask DisposeAsync()
  {
    await _cts.CancelAsync();
    await _flushTask;

    Flush();

    _flushTimer.Dispose();
    _cts.Dispose();
    await _data.DisposeAsync();
    await _dataindex.DisposeAsync();
  }
}
