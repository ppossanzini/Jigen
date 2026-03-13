namespace Jigen.Persistance;

public partial class StoredList<T> : IList<T> where T : IStorableItem<T>
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


  public void Flush()
  {
    _itemsIndexLock.EnterReadLock();
    try
    {
      WriteIndex();
      RandomAccess.Write(_data.SafeFileHandle!, _header.HeaderData.AsSpan(), 0);
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