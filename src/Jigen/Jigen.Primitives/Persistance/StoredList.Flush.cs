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
        try
        {
          Flush();
        }
        catch
        {
          // A transient I/O failure must not kill the loop: without it the
          // periodic flushes stop silently and durability degrades unnoticed.
          // The flush is retried on the next tick.
        }
      }
    }
    catch (OperationCanceledException)
    {
    }
  }

  public void Flush()
  {
    // Upgradeable read: concurrent with plain readers, exclusive against
    // writers AND other flushes (the dirty bookkeeping is consumed here).
    _itemsIndexLock.EnterUpgradeableReadLock();
    try
    {
      var count = _itemsIndex.Count;

      // Incremental flush: entries below the watermark are on disk already;
      // rewrite only the in-place updates and append the new tail. Fall back
      // to one full write when the dirty set makes scattered writes costlier.
      if (_dirtyIndexes.Count > 1024 || (long)_dirtyIndexes.Count * 4 > count)
      {
        WriteIndex();
      }
      else
      {
        foreach (var index in _dirtyIndexes)
          if (index < count)
            WriteIndexAt(index);

        WriteIndexRange(_flushedCount, count);
      }

      _dirtyIndexes.Clear();
      _flushedCount = count;
      WriteHeader();
    }
    finally
    {
      _itemsIndexLock.ExitUpgradeableReadLock();
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
