using Jigen.Extensions;

namespace Jigen;

public class Writer
{
  private volatile bool _running = true;

  private readonly Thread _writingThread;
  private readonly Thread _flusher;

  private readonly AutoResetEvent _waiter = new(false);
  private readonly ManualResetEvent _writingCompleted = new(true);
  private readonly AutoResetEvent _flushWake = new(false);

  private readonly object _ioLock = new();
  private readonly Store _store;

  public Task WaitForWritingCompleted => Task.Run(() => _writingCompleted.WaitOne());

  public Writer(Store store)
  {
    _store = store;
    _writingThread = new Thread(WriterJob) { IsBackground = true };
    _writingThread.Start();

    _flusher = new Thread(FlushJob) { IsBackground = true };
    _flusher.Start();
  }

  internal void SignalNewData()
  {
    _writingCompleted.Reset();
    _waiter.Set();
  }


  private void FlushJob()
  {
    while (_running)
    {
      // Wait 30s or until Stop() wakes us
      _flushWake.WaitOne(TimeSpan.FromSeconds(30));
      if (!_running) break;

      // Flush only when writer is idle
      if (!_writingCompleted.WaitOne(0)) continue;

      lock (_ioLock)
      {
        _store.EmbeddingFileStream.FlushAsync().GetAwaiter().GetResult();
        _store.ContentFileStream.FlushAsync().GetAwaiter().GetResult();
        _store.IndexFileStream.FlushAsync().GetAwaiter().GetResult();
      }
    }
  }

  private void WriterJob()
  {
    while (_running)
    {
      _waiter.WaitOne(TimeSpan.FromMilliseconds(200));
      if (!_running) break;

      if (_store.IngestionQueue.IsEmpty)
      {
        _writingCompleted.Set();
        continue;
      }

      try
      {
        lock (_ioLock)
        {
          while (_store.IngestionQueue.TryDequeue(out var entry))
          {
            _store.AppendContent(entry.Id, entry.CollectionName, entry.Content, entry.Embedding).GetAwaiter().GetResult();
          }
        }
      }
      finally
      {
        if (_store.IngestionQueue.IsEmpty)
          _writingCompleted.Set();
      }
    }

    _writingCompleted.Set();
  }

  public void Stop()
  {
    _running = false;

    _waiter.Set();
    _flushWake.Set();

    _writingThread.Join();
    _flusher.Join();
  }
}