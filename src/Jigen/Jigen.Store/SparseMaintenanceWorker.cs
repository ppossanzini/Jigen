namespace Jigen;

/// <summary>
/// Background thread that, every <see cref="SparseOptions.MaintenanceInterval"/>,
/// commits the sparse buffers older than <see cref="SparseOptions.CommitMaxDelay"/>
/// and compacts the collections reading more than
/// <see cref="SparseOptions.MaxSegments"/> segments.
/// </summary>
internal sealed class SparseMaintenanceWorker
{
  private readonly Store _store;
  private readonly TimeSpan _interval;
  private readonly AutoResetEvent _wake = new(false);
  private readonly Thread _thread;
  private volatile bool _running = true;

  public SparseMaintenanceWorker(Store store, TimeSpan interval)
  {
    _store = store;
    _interval = interval;
    _thread = new Thread(Loop) { IsBackground = true, Name = "Jigen sparse maintenance" };
    _thread.Start();
  }

  /// <summary>Last failure of a maintenance pass, if any.</summary>
  public Exception LastError { get; private set; }

  public void Stop()
  {
    _running = false;
    _wake.Set();
    _thread.Join();
  }

  private void Loop()
  {
    while (_running)
    {
      _wake.WaitOne(_interval);
      if (!_running) break;

      try
      {
        _store.RunSparseMaintenance();
        LastError = null;
      }
      catch (Exception ex)
      {
        // Retried on the next tick: the segments and the buffers are intact.
        LastError = ex;
      }
    }
  }
}
