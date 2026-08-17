using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
  // ── WAL write helpers ──

  /// <summary>
  /// Serializes and appends an Insert record to the WAL. The caller completes
  /// the configured durability policy while still holding Store.WalLock.
  /// Called BEFORE the entry is enqueued to the ingestion queue — this is the
  /// "write-ahead" guarantee.
  /// </summary>
  private static void WriteWalInsert(Store store, VectorEntry entry)
  {
    var walStream = store.WalFileStream!;
    var content = entry.Content.IsEmpty ? null : entry.Content.ToArray();
    var embedding = entry.Embedding.IsEmpty ? null : entry.Embedding.ToArray();

    int size = WalRecord.InsertRecordSize(entry.Id, entry.CollectionName, content, embedding);
    byte[] rented = null;
    Span<byte> buffer = size <= 8192
      ? stackalloc byte[size]
      : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

    try
    {
      WalRecord.SerializeInsert(buffer, entry.Id, entry.CollectionName, content, embedding);
      walStream.Write(buffer[..size]);
    }
    finally
    {
      if (rented != null) ArrayPool<byte>.Shared.Return(rented);
    }
  }

  private static void WriteWalDelete(Store store, byte[] key, string collection)
  {
    var walStream = store.WalFileStream!;
    int size = WalRecord.DeleteRecordSize(key, collection);
    byte[] rented = null;
    Span<byte> buffer = size <= 512
      ? stackalloc byte[size]
      : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

    try
    {
      WalRecord.SerializeDelete(buffer, key, collection);
      walStream.Write(buffer[..size]);
    }
    finally
    {
      if (rented != null) ArrayPool<byte>.Shared.Return(rented);
    }
  }

  private static void WriteWalClearCollection(Store store, string collection)
  {
    var walStream = store.WalFileStream!;
    int size = WalRecord.ClearCollectionRecordSize(collection);
    byte[] rented = null;
    Span<byte> buffer = size <= 512
      ? stackalloc byte[size]
      : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

    try
    {
      WalRecord.SerializeClearCollection(buffer, collection);
      walStream.Write(buffer[..size]);
    }
    finally
    {
      if (rented != null) ArrayPool<byte>.Shared.Return(rented);
    }
  }

  // Called while Store.WalLock is held, after the complete WAL record/block is
  // appended and before its operation is exposed to the live store.
  internal static void CompleteWalWrite(Store store, int writes = 1)
  {
    var wal = store.WalFileStream!;
    switch (store.Options.Wal!.Durability)
    {
      case WalDurability.PerWrite:
        wal.Flush(true);
        return;
      case WalDurability.None:
        wal.Flush(false);
        return;
      case WalDurability.Group:
        wal.Flush(false);
        break;
    }

    // Called while Store.WalLock is held. Counters are per store: a busy
    // database must not reset the group-commit policy of another database.
    store.WalGroupCounter += writes;

    if (store.WalGroupCounter >= store.Options.Wal.MaxGroupBatchCount
        || store.WalGroupTimer.Elapsed >= store.Options.Wal.MaxGroupDelay)
    {
      wal.Flush(true);
      store.WalGroupCounter = 0;
      store.WalGroupTimer.Restart();
    }
  }

  internal static void AppendIndex(
    this Store store,
    (byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize) item)
  {
    lock (store.IndexAppendLock)
    {
      var collectionIndex = store.PositionIndex.GetOrAdd(item.collectioname,
        _ => new ConcurrentDictionary<byte[], (long, long, int, long)>(ByteArrayEqualityComparer.Instance));

      // Overwrites leave the previous record unreachable: account it as dead space.
      if (collectionIndex.TryGetValue(item.id, out var old))
      {
        if (old.Item1 > 0 && old.Item1 != item.contentposition)
          store.DeadContentBytes += Store.ContentRecordSize(item.id.Length, old.Item4);
        if (old.Item2 > 0 && old.Item2 != item.embeddingposition)
          store.DeadEmbeddingBytes += Store.EmbeddingRecordSize(item.id.Length, old.Item3);
      }

      collectionIndex[item.id] = (item.contentposition, item.embeddingposition, item.dimensions, item.contentsize);

      WriteIndexRecord(store.IndexFileStream, item.id, item.collectioname, item.contentposition, item.embeddingposition, item.dimensions, item.contentsize);
    }
  }

  internal static void WriteIndexRecord(FileStream file, byte[] id, string collection, long contentposition, long embeddingposition, int dimensions, long contentsize)
  {
    // The record is assembled in a buffer and written with a single Write:
    // seven separate writes left a wide window for a crash to interleave
    // partial fields. A crash can still tear the single write, but only into
    // a short tail that LoadIndex detects and truncates.
    var nameByteCount = Encoding.UTF8.GetByteCount(collection);
    var size = 2 * sizeof(int) + id.Length + nameByteCount + 2 * sizeof(long) + sizeof(int) + sizeof(long);

    byte[] rented = null;
    Span<byte> buffer = size <= 512
      ? stackalloc byte[size]
      : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

    try
    {
      var offset = 0;
      BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), id.Length);
      offset += sizeof(int);
      id.CopyTo(buffer.Slice(offset));
      offset += id.Length;
      BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), nameByteCount);
      offset += sizeof(int);
      Encoding.UTF8.GetBytes(collection, buffer.Slice(offset));
      offset += nameByteCount;
      BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), contentposition);
      offset += sizeof(long);
      BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), embeddingposition);
      offset += sizeof(long);
      BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), dimensions);
      offset += sizeof(int);
      BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), contentsize);

      file.Seek(0, SeekOrigin.End);
      file.Write(buffer);
    }
    finally
    {
      if (rented != null)
        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
    }
  }


  public static async Task<VectorEntry> AppendContent(this Store store, VectorEntry entry)
  {
    store.ValidateVectorDimensions(entry);
    // WAL: write BEFORE enqueuing. This is the write-ahead guarantee.
    if (store.Options.Wal?.Enabled == true)
    {
      lock (store.WalLock)
      {
        WriteWalInsert(store, entry);
        store.IngestionQueue.Enqueue(entry);
        store.Writer.SignalNewData();
        CompleteWalWrite(store);
      }
      return entry;
    }

    await store.IngestionQueue.EnqueueAsync(entry);
    store.Writer.SignalNewData();
    return entry;
  }

  /// <summary>
  /// Bulk ingestion: enqueues multiple entries with reduced queue overhead.
  /// All entries share one semaphore acquisition batch for the ingestion queue.
  /// </summary>
  public static async Task AppendContentBulk(this Store store, IReadOnlyList<VectorEntry> entries)
  {
    // Validate the whole batch before accepting its first WAL/data operation;
    // a bad dimension must not turn a bulk call into a partial write.
    foreach (var entry in entries)
      store.ValidateVectorDimensions(entry);

    // Batch-enqueue: acquire semaphore once per window instead of per entry.
    // The queue capacity is 1M, so we can enqueue large batches safely.
    const int windowSize = 256;
    for (int offset = 0; offset < entries.Count; offset += windowSize)
    {
      var window = Math.Min(windowSize, entries.Count - offset);
      if (store.Options.Wal?.Enabled == true)
      {
        lock (store.WalLock)
        {
          for (int i = 0; i < window; i++)
          {
            var entry = entries[offset + i];
            WriteWalInsert(store, entry);
            store.IngestionQueue.Enqueue(entry);
          }
          store.Writer.SignalNewData(window);
          CompleteWalWrite(store, window);
        }
      }
      else
      {
        for (int i = 0; i < window; i++)
          await store.IngestionQueue.EnqueueAsync(entries[offset + i]);
        store.Writer.SignalNewData(window);
      }
    }
  }

  public static Task<VectorEntry> SetContent(this Store store, VectorEntry entry)
  {
    return store.AppendContent(entry);
  }

  public static async Task<bool> DeleteContent(this Store store, string collection, byte[] key)
  {
    if (store.Options.Wal?.Enabled == true)
    {
      lock (store.WalLock)
      {
        DrainPipeline(store);
        WriteWalDelete(store, key, collection);
        CompleteWalWrite(store);
        return DeleteContentCore(store, collection, key);
      }
    }

    // Appends travel through the ingestion queue while deletes run inline:
    // drain BOTH stages first, so "append X, then delete X" cannot resurrect
    // X in the store (writer) or in the graph (index workers).
    await store.Writer.WaitForWritingCompleted;
    await store.Writer.WaitForIndexingCompleted;
    return DeleteContentCore(store, collection, key);
  }

  internal static void DrainPipeline(Store store)
  {
    store.Writer.WaitForWritingCompleted.GetAwaiter().GetResult();
    store.Writer.WaitForIndexingCompleted.GetAwaiter().GetResult();
  }

  internal static bool DeleteContentCore(Store store, string collection, byte[] key)
  {

    bool result = false;

    lock (store.IndexAppendLock)
    {
      if (store.PositionIndex.TryGetValue(collection, out var index) &&
          index.TryRemove(key, out var old))
      {
        if (old.contentposition > 0)
          store.DeadContentBytes += Store.ContentRecordSize(key.Length, old.size);
        if (old.embeddingsposition > 0)
          store.DeadEmbeddingBytes += Store.EmbeddingRecordSize(key.Length, old.dimensions);

        // Tombstone record: LoadIndex replays the log and removes the key,
        // so the deletion survives a restart.
        WriteIndexRecord(store.IndexFileStream, key, collection, Store.IndexTombstone, Store.IndexTombstone, 0, 0);
        result = true;
      }
    }

    if (result)
    {
      store.Options.Indexer?.RemoveFromIndex(collection, key);
      // Group commit: no per-delete fsync. The tombstone becomes durable at
      // the next SaveChangesAsync/Close, exactly like appended entries.
    }

    return result;
  }

  /// <summary>
  /// Deletes every entry of a collection, persisting the deletions as tombstone
  /// records so they survive a restart. Returns the number of entries removed.
  /// </summary>
  public static async Task<int> ClearContent(this Store store, string collection)
  {
    if (store.Options.Wal?.Enabled == true)
    {
      lock (store.WalLock)
      {
        DrainPipeline(store);
        WriteWalClearCollection(store, collection);
        CompleteWalWrite(store);
        return ClearContentCore(store, collection);
      }
    }

    // Same ordering guarantee as DeleteContent: queued appends must land
    // (and index) before the clear, or they would resurrect after it.
    await store.Writer.WaitForWritingCompleted;
    await store.Writer.WaitForIndexingCompleted;

    return ClearContentCore(store, collection);
  }

  private static int ClearContentCore(Store store, string collection)
  {

    var removedKeys = new List<byte[]>();

    lock (store.IndexAppendLock)
    {
      if (store.PositionIndex.TryRemove(collection, out var index))
      {
        foreach (var (key, old) in index)
        {
          if (old.contentposition > 0)
            store.DeadContentBytes += Store.ContentRecordSize(key.Length, old.size);
          if (old.embeddingsposition > 0)
            store.DeadEmbeddingBytes += Store.EmbeddingRecordSize(key.Length, old.dimensions);

          WriteIndexRecord(store.IndexFileStream, key, collection, Store.IndexTombstone, Store.IndexTombstone, 0, 0);
          removedKeys.Add(key);
        }
      }
    }

    if (removedKeys.Count > 0)
    {
      foreach (var key in removedKeys)
        store.Options.Indexer?.RemoveFromIndex(collection, key);

      // Push the tombstone burst to the OS without fsync: durability comes
      // with the next SaveChangesAsync/Close (group commit).
      store.IndexFileStream.Flush(false);
    }

    return removedKeys.Count;
  }

  /// <summary>
  /// Batch-serializes multiple entries into pooled buffers and writes them with
  /// one <see cref="FileStream.Write(byte[],int,int)"/> per file, eliminating
  /// per-entry Seek and tiny Write kernel calls. Returns position tuples in
  /// the same order as the input list.
  /// </summary>
  internal static List<(byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize)>
    AppendContentBatch(this Store store, List<VectorEntry> entries)
  {
    var results = new List<(byte[], string, long, long, int, long)>(entries.Count);
    if (entries.Count == 0) return results;

    // ── Calculate total byte sizes ──
    long contentTotal = 0, embeddingTotal = 0;
    foreach (var e in entries)
    {
      if (e.Content.Length > 0)
        contentTotal += Store.ContentRecordSize(e.Id.Length, e.Content.Length);
      if (e.Embedding.Length > 0)
        embeddingTotal += Store.EmbeddingRecordSize(e.Id.Length, e.Embedding.Length);
    }

    // ── Allocate pooled buffers ──
    byte[] contentBuf = null!, embedBuf = null!;
    int contentOff = 0, embedOff = 0;

    if (contentTotal > 0)
    {
      contentBuf = System.Buffers.ArrayPool<byte>.Shared.Rent((int)contentTotal);
      contentOff = 0;
    }
    if (embeddingTotal > 0)
    {
      embedBuf = System.Buffers.ArrayPool<byte>.Shared.Rent((int)embeddingTotal);
      embedOff = 0;
    }

    try
    {
      // ── Acquire file positions once ──
      var contentStream = store.ContentFileStream;
      var embedStream = store.EmbeddingFileStream;

      long contentBasePos = contentTotal > 0 ? contentStream.Seek(0, SeekOrigin.End) : 0;
      long embedBasePos = embeddingTotal > 0 ? embedStream.Seek(0, SeekOrigin.End) : 0;

      // ── Serialize all entries into buffers ──
      foreach (var entry in entries)
      {
        long cp = 0, ep = 0;

        if (entry.Content.Length > 0)
        {
          cp = contentBasePos + contentOff;
          var span = contentBuf.AsSpan(contentOff);
          BinaryPrimitives.WriteInt32LittleEndian(span, entry.Id.Length);
          contentOff += 4;
          entry.Id.CopyTo(contentBuf.AsSpan(contentOff));
          contentOff += entry.Id.Length;
          BinaryPrimitives.WriteInt32LittleEndian(contentBuf.AsSpan(contentOff), entry.Content.Length);
          contentOff += 4;
          entry.Content.Span.CopyTo(contentBuf.AsSpan(contentOff));
          contentOff += entry.Content.Length;
        }

        if (entry.Embedding.Length > 0)
        {
          ep = embedBasePos + embedOff;
          var span = embedBuf.AsSpan(embedOff);
          BinaryPrimitives.WriteInt32LittleEndian(span, entry.Id.Length);
          embedOff += 4;
          entry.Id.CopyTo(embedBuf.AsSpan(embedOff));
          embedOff += entry.Id.Length;
          System.Runtime.InteropServices.MemoryMarshal.AsBytes(entry.Embedding.Span)
            .CopyTo(embedBuf.AsSpan(embedOff));
          embedOff += entry.Embedding.Length * sizeof(float);
        }

        results.Add((entry.Id, entry.CollectionName, cp, ep,
          entry.Embedding.Length, entry.Content.Length));
      }

      // ── Single Write per file ──
      if (contentTotal > 0)
      {
        contentStream.Write(contentBuf, 0, contentOff);
        store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
      }
      if (embeddingTotal > 0)
      {
        embedStream.Write(embedBuf, 0, embedOff);
        store.VectorStoreHeader.EmbeddingCurrentPosition = embedStream.Position;
      }
    }
    finally
    {
      if (contentTotal > 0)
        System.Buffers.ArrayPool<byte>.Shared.Return(contentBuf);
      if (embeddingTotal > 0)
        System.Buffers.ArrayPool<byte>.Shared.Return(embedBuf);
    }

    return results;
  }

  internal static (byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize)
    AppendContent(this Store store, byte[] id, string collection, ReadOnlyMemory<byte>? content, ReadOnlyMemory<float>? embeddings)
  {
    (long contentPosition, long embeddingPosition, int dimensions, long size) actualindex = default;

    if (content?.Length == 0) content = null;
    if (embeddings?.Length == 0) embeddings = null;

    if (content == null || embeddings == null)
    {
      store.PositionIndex.TryGetValue(collection, out var positionIndex);
      positionIndex?.TryGetValue(id, out actualindex);
    }

    var contentPosition = actualindex.contentPosition;
    if (content != null)
    {
      var contentStream = store.ContentFileStream;

      contentStream.Seek(0, SeekOrigin.End);
      contentPosition = contentStream.Position;

      contentStream.WriteInt32Le(id.Length);
      contentStream.Write(id, 0, id.Length);
      contentStream.WriteInt32Le(content.Value.Length);
      
      contentStream.Write(content.Value.Span);

      store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
    }

    var embeddingPosition = actualindex.embeddingPosition;
    if (embeddings != null)
    {
      var embeddingsStream = store.EmbeddingFileStream;
      embeddingsStream.Seek(0, SeekOrigin.End);
      embeddingPosition = embeddingsStream.Position;

      embeddingsStream.WriteInt32Le(id.Length);
      embeddingsStream.Write(id, 0, id.Length);

      embeddingsStream.WriteByteArray(embeddings.Value.Span);
      
      store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;
    }

    return (id, collection, contentPosition, embeddingPosition, embeddings?.Length ?? actualindex.dimensions, content?.Length ?? actualindex.size);
  }
}
