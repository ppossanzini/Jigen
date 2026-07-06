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
        _store.EmbeddingFileStream.Flush(false);
        _store.ContentFileStream.Flush(false);
        _store.IndexFileStream.Flush(false);
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

            // waitForIndexing: the writer thread is already sequential; the default
            // fire-and-forget path runs inserts concurrently on the thread pool and
            // HNSW graph construction is not safe under concurrent inserts.
            _store.Options.Indexer?.AddToIndex(new VectorEntry()
            {
              Id = result.id, CollectionName = entry.CollectionName, Embedding = entry.Embedding, Content = entry.Content
            }, waitForIndexing: true);
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