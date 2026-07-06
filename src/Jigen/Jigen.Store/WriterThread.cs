using System.Text;
using Jigen.DataStructures;
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

  private readonly Lock _ioLock = new();
  private readonly Store _store;

  private Queue<(byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize)> TempIndex = new();

  // Last failure recorded by the background writer or the indexer. The writer
  // runs on a raw thread: an exception must never escape it (it would kill the
  // whole process) nor break its loop (a stalled writer blocks producers on a
  // full queue and hangs Stop()). Failures are recorded here instead and
  // surfaced by Store.SaveChangesAsync.
  private Exception _lastError;

  internal Exception LastError => Volatile.Read(ref _lastError);

  internal Exception TakePendingError() => Interlocked.Exchange(ref _lastError, null);

  private void RecordError(Exception ex) => Volatile.Write(ref _lastError, ex);

  public Task WaitForWritingCompleted
  {
    get
    {
      // Fast path: no writes in flight, nothing to wait for.
      if (_writingCompleted.WaitOne(0)) return Task.CompletedTask;

      // Bridge the event to a Task via the thread pool's shared wait threads:
      // unlike Task.Run(() => WaitOne()), no pool thread is blocked per caller.
      var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      var registration = ThreadPool.RegisterWaitForSingleObject(
        _writingCompleted,
        static (state, _) => ((TaskCompletionSource)state!).TrySetResult(),
        tcs, Timeout.Infinite, executeOnlyOnce: true);

      tcs.Task.ContinueWith(
        static (_, state) => ((RegisteredWaitHandle)state!).Unregister(null),
        registration, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

      return tcs.Task;
    }
  }

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
        try
        {
          _store.EmbeddingFileStream.Flush(false);
          _store.ContentFileStream.Flush(false);
          _store.IndexFileStream.Flush(false);
        }
        catch
        {
          // Transient I/O failure: retried on the next tick. The flusher
          // thread must survive, like the writer.
        }
      }
    }
  }

  private void WriterJob()
  {
    // Keep processing after Stop() until the queue is drained: items accepted
    // by AppendContent must reach the files even when Close follows immediately.
    while (_running || !_store.IngestionQueue.IsEmpty)
    {
      if (_store.IngestionQueue.IsEmpty)
      {
        _writingCompleted.Set();
        _waiter.WaitOne(TimeSpan.FromMilliseconds(200));
        continue;
      }

      // The whole batch (content/embedding writes AND index appends) runs
      // inside _ioLock so that anyone holding the lock (flusher, ShrinkAsync)
      // observes a consistent state: no content bytes without their index entry.
      try
      {
        lock (_ioLock)
        {
          try
          {
            while (_store.IngestionQueue.TryDequeue(out var entry))
            {
              var result = _store.AppendContent(
                entry.Id,
                entry.CollectionName,
                entry.Content,
                entry.Embedding);

              // Cannot immediatly update search index till the file are committed
              TempIndex.Enqueue(result);

              try
              {
                // waitForIndexing: the writer thread is already sequential; the default
                // fire-and-forget path runs inserts concurrently on the thread pool and
                // HNSW graph construction is not safe under concurrent inserts.
                _store.Options.Indexer?.AddToIndex(new VectorEntry()
                {
                  Id = result.id, CollectionName = entry.CollectionName, Embedding = entry.Embedding, Content = entry.Content
                }, waitForIndexing: true);
              }
              catch (Exception ex)
              {
                // The entry is persisted in the store (its index record is in
                // TempIndex); only the vector index missed it. Keep the batch going.
                RecordError(ex);
              }
            }
          }
          finally
          {
            _store.EmbeddingFileStream.Flush(false);
            _store.ContentFileStream.Flush(false);

            _store.EnableReading();

            while (TempIndex.TryDequeue(out var indexData))
              _store.AppendIndex(indexData);

            _store.IndexFileStream.Flush(false);
          }
        }
      }
      catch (Exception ex)
      {
        // Disk full, I/O failure, a poison entry: the batch is lost but the
        // writer keeps draining the queue. Never let an exception escape.
        RecordError(ex);
      }

      if (_store.IngestionQueue.IsEmpty)
        _writingCompleted.Set();
    }

    _writingCompleted.Set();
  }

  /// <summary>
  /// Runs an action while holding the writer's I/O lock: the writer thread and
  /// the flusher cannot touch the store files for the duration of the action.
  /// </summary>
  internal void RunExclusive(Action action)
  {
    lock (_ioLock)
      action();
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