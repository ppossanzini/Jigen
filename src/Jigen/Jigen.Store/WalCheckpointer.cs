namespace Jigen;

/// <summary>
/// Background thread that periodically writes checkpoint markers to the WAL
/// and truncates records already flushed to the data files. The WAL is written
/// BEFORE the ingestion queue (by StoreWritingExtensions), so the data files
/// are always in sync with the WAL — the checkpointer only fsyncs and truncates.
/// </summary>
public class WalCheckpointer
{
  private readonly Store _store;
  private readonly Writer _writer;
  private readonly ManualResetEvent _completed = new(true);
  private readonly AutoResetEvent _forceWake = new(false);
  private volatile bool _running = true;

  private long _checkpointedWalPosition;

  public WalCheckpointer(Store store)
  {
    _store = store;
    _writer = store.Writer;
    _checkpointedWalPosition = store.CheckpointedWalPosition;

    var thread = new Thread(CheckpointLoop) { IsBackground = true };
    thread.Start();
  }

  /// <summary>
  /// Forces an immediate, synchronous checkpoint: wait for the writer to
  /// drain, fsync data files, write checkpoint marker, truncate WAL.
  /// </summary>
  public void ForceCheckpoint()
  {
    _completed.Reset();
    _forceWake.Set();
    SpinWait.SpinUntil(() => _completed.WaitOne(0), TimeSpan.FromSeconds(10));
    _store.CheckpointedWalPosition = Volatile.Read(ref _checkpointedWalPosition);
  }

  public void Stop()
  {
    _running = false;
    _forceWake.Set();
    _completed.Set();
  }

  public void ResetPosition()
  {
    Volatile.Write(ref _checkpointedWalPosition, 0);
  }

  private void CheckpointLoop()
  {
    while (_running)
    {
      WaitHandle.WaitAny(
        [_forceWake, _completed],
        _store.Options.Wal!.CheckpointInterval);

      if (!_running) break;

      try
      {
        PerformCheckpoint();
      }
      catch
      {
        // Retry on next tick — the WAL still holds everything.
      }
      finally
      {
        _completed.Set();
      }
    }
  }

  private void PerformCheckpoint()
  {
    // Exclude new accepted operations for the whole drain + fsync + checkpoint
    // sequence. Append/delete register their work while holding the same lock.
    lock (_store.WalLock)
    {
      // Wait for the ingestion pipeline to fully drain BEFORE we fsync and
      // truncate the WAL. Otherwise WAL records still in the IngestionQueue
      // (not yet written to data files) would be lost.
      _writer.WaitForWritingCompleted.GetAwaiter().GetResult();
      _writer.WaitForIndexingCompleted.GetAwaiter().GetResult();

      _writer.RunExclusive(() =>
      {
        lock (_store.IndexAppendLock)
        {
          PerformCheckpointCore();
        }
      });

      _store.CheckpointedWalPosition = Volatile.Read(ref _checkpointedWalPosition);
    }
  }

  private void PerformCheckpointCore()
  {
    var walStream = _store.WalFileStream;
    if (walStream is not { CanRead: true }) return;

    // Make sure the data files are durable before we truncate the WAL.
    _store.ContentFileStream!.Flush(true);
    _store.EmbeddingFileStream!.Flush(true);
    _store.IndexFileStream!.Flush(true);

    // Write a checkpoint marker at the current WAL tail.
    Span<byte> marker = stackalloc byte[WalRecord.CheckpointMarkerSize];
    WalRecord.SerializeCheckpoint(marker);
    walStream.Write(marker);
    walStream.Flush(true);

    // Truncate: everything up to and including the checkpoint marker is
    // durable in the data files — the WAL can be discarded.
    long truncatePosition = walStream.Position;
    walStream.SetLength(truncatePosition);
    walStream.Flush(true);
    walStream.Seek(0, SeekOrigin.End);

    _checkpointedWalPosition = walStream.Position;
  }
}
