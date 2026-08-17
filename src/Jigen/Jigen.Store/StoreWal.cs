using Jigen.DataStructures;
using Jigen.Extensions;


namespace Jigen;

public partial class Store
{
  /// <summary>
  /// Applies WAL records written after the last checkpoint on top of the
  /// PositionIndex already loaded by <see cref="LoadIndex"/>.
  /// Called once during construction, after LoadIndex.
  ///
  /// Transaction handling: when a <see cref="WalRecordType.BeginTransaction"/>
  /// marker is encountered, subsequent records are buffered until the matching
  /// <see cref="WalRecordType.CommitTransaction"/>. Only then are they applied
  /// atomically. If the WAL ends without a COMMIT, the buffered records are
  /// discarded (rolled back) and the WAL is truncated before the BEGIN marker.
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

    // 3. Feed records into the ingestion pipeline, respecting transactions.
    long lastValidPosition = walStream.Position;

    // Transaction state: when non-null, we're inside a transaction.
    List<(WalRecordType Type, VectorEntry Entry, byte[] Key, string Collection)> txOperations = null;
    long txBeginPosition = 0;

    while (WalRecord.TryReadRecord(walStream,
             out var type, out var id, out var collection,
             out var content, out var embedding, out var txId, out _))
    {
      switch (type)
      {
        case WalRecordType.BeginTransaction:
          // Nested transactions not supported: if already in one, treat as error
          // and stop replay at the previous valid position.
          if (txOperations is not null)
          {
            walStream.Seek(lastValidPosition, SeekOrigin.Begin);
            goto done;
          }
          txOperations = new List<(WalRecordType, VectorEntry, byte[], string)>();
          txBeginPosition = lastValidPosition;
          lastValidPosition = walStream.Position;
          break;

        case WalRecordType.CommitTransaction:
          if (txOperations is null)
          {
            // Orphan COMMIT without BEGIN: skip it, keep lastValidPosition.
            break;
          }
          // Only the last operation for a collection/key affects final state.
          // This preserves insert→delete and delete→insert semantics without
          // requiring the writer (which is constructed after WAL replay).
          var seen = new HashSet<(string Collection, VectorKey Key)>();
          for (var i = txOperations.Count - 1; i >= 0; i--)
          {
            var operation = txOperations[i];
            var identity = (operation.Collection,
              new VectorKey { Value = operation.Type == WalRecordType.Insert
                ? operation.Entry.Id
                : operation.Key });
            if (!seen.Add(identity)) continue;

            if (operation.Type == WalRecordType.Insert)
            {
              ValidateVectorDimensions(operation.Entry);
              IngestionQueue.Enqueue(operation.Entry);
            }
            else
              ApplyWalDelete(operation.Key, operation.Collection);
          }
          txOperations = null;
          lastValidPosition = walStream.Position;
          break;

        case WalRecordType.Insert:
          if (txOperations is not null)
          {
            // Inside a transaction: buffer.
            var entry = new VectorEntry
            {
              Id = id,
              CollectionName = collection,
              Content = content ?? [],
              Embedding = embedding ?? []
            };
            txOperations.Add((WalRecordType.Insert, entry, null, collection));
          }
          else
          {
            // Outside a transaction: apply immediately (backward compat).
            var entry = new VectorEntry
            {
              Id = id,
              CollectionName = collection,
              Content = content ?? [],
              Embedding = embedding ?? []
            };
            ValidateVectorDimensions(entry);
            IngestionQueue.Enqueue(entry);
            lastValidPosition = walStream.Position;
          }
          break;

        case WalRecordType.Delete:
          if (txOperations is not null)
          {
            // Inside a transaction: buffer.
            txOperations.Add((WalRecordType.Delete, null, id, collection));
          }
          else
          {
            // Outside a transaction: apply immediately.
            ApplyWalDelete(id, collection);
            lastValidPosition = walStream.Position;
          }
          break;

        case WalRecordType.ClearCollection:
          // ClearCollection inside a transaction is not supported:
          // if inside a tx, stop replay before this record.
          if (txOperations is not null)
          {
            walStream.Seek(lastValidPosition, SeekOrigin.Begin);
            goto done;
          }
          ApplyWalClearCollection(collection);
          lastValidPosition = walStream.Position;
          break;

        case WalRecordType.Checkpoint:
          break;
      }
    }

    // 4. If we ended mid-transaction, roll back: truncate before the BEGIN.
    if (txOperations is not null)
    {
      walStream.Seek(txBeginPosition, SeekOrigin.Begin);
      lastValidPosition = txBeginPosition;
    }

done:
    // Truncate torn writes (CRC mismatch or incomplete transaction).
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
