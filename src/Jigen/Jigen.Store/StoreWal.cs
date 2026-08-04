using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public partial class Store
{
  /// <summary>
  /// Applies WAL records written after the last checkpoint on top of the
  /// PositionIndex already loaded by <see cref="LoadIndex"/>.
  /// Called once during construction, after LoadIndex.
  /// </summary>
  private void ReplayWal()
  {
    var walStream = WalFileStream;
    if (walStream is not { CanRead: true, Length: > 0 })
      return;

    // 1. Scan forward to find the last checkpoint marker.
    long lastCheckpoint = FindLastCheckpointForward(walStream);

    // 2. Seek to right after the checkpoint (or beginning if none).
    walStream.Seek(lastCheckpoint, SeekOrigin.Begin);

    // 2. Feed records into the ingestion pipeline.
    long lastValidPosition = walStream.Position;

    while (WalRecord.TryReadRecord(walStream,
             out var type, out var id, out var collection,
             out var content, out var embedding, out _))
    {
      lastValidPosition = walStream.Position;

      switch (type)
      {
        case WalRecordType.Insert:
          // Enqueue into the IngestionQueue — the WriterThread (started later
          // in the constructor) processes it and writes to content/vectors/index.
          // No data-file write happens here: only the WriterThread touches those.
          IngestionQueue.Enqueue(new VectorEntry
          {
            Id = id,
            CollectionName = collection,
            Content = content ?? [],
            Embedding = embedding ?? []
          });
          Writer.SignalNewData();
          break;

        case WalRecordType.Delete:
          ApplyWalDelete(id, collection);
          break;

        case WalRecordType.ClearCollection:
          ApplyWalClearCollection(collection);
          break;

        case WalRecordType.Checkpoint:
          break;
      }
    }

    // Truncate torn writes (CRC mismatch stops the loop).
    walStream.SetLength(lastValidPosition);
    walStream.Flush(true);

    CheckpointedWalPosition = walStream.Position;
  }

  private static long FindLastCheckpointForward(FileStream walStream)
  {
    long found = 0;
    walStream.Seek(0, SeekOrigin.Begin);

    while (WalRecord.TryReadRecord(walStream,
             out var type, out _, out _, out _, out _, out _))
    {
      if (type == WalRecordType.Checkpoint)
        found = walStream.Position;
    }

    return found;
  }

  /// <summary>
  /// Removes a key from PositionIndex and writes a tombstone to index.jigen.
  /// Does NOT touch content.jigen or vectors.jigen — their only writer is the WriterThread.
  /// </summary>
  private void ApplyWalDelete(byte[] id, string collection)
  {
    lock (IndexAppendLock)
    {
      if (PositionIndex.TryGetValue(collection, out var ci) &&
          ci.TryRemove(id, out var old))
      {
        if (old.contentposition > 0)
          DeadContentBytes += ContentRecordSize(id.Length, old.size);
        if (old.embeddingsposition > 0)
          DeadEmbeddingBytes += EmbeddingRecordSize(id.Length, old.dimensions);

        StoreWritingExtensions.WriteIndexRecord(IndexFileStream, id, collection,
          IndexTombstone, IndexTombstone, 0, 0);
      }
    }
  }

  private void ApplyWalClearCollection(string collection)
  {
    lock (IndexAppendLock)
    {
      if (PositionIndex.TryRemove(collection, out var index))
      {
        foreach (var (key, old) in index)
        {
          if (old.contentposition > 0)
            DeadContentBytes += ContentRecordSize(key.Length, old.size);
          if (old.embeddingsposition > 0)
            DeadEmbeddingBytes += EmbeddingRecordSize(key.Length, old.dimensions);

          StoreWritingExtensions.WriteIndexRecord(IndexFileStream, key, collection,
            IndexTombstone, IndexTombstone, 0, 0);
        }
      }

      IndexFileStream.Flush(false);
    }
  }
}
