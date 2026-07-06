using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
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
    await store.IngestionQueue.EnqueueAsync(entry);
    store.Writer.SignalNewData();
    return entry;
  }

  public static Task<VectorEntry> SetContent(this Store store, VectorEntry entry)
  {
    return store.AppendContent(entry);
  }

  public static async Task<bool> DeleteContent(this Store store, string collection, byte[] key)
  {
    // Appends travel through the ingestion queue while deletes run inline:
    // drain BOTH stages first, so "append X, then delete X" cannot resurrect
    // X in the store (writer) or in the graph (index workers).
    await store.Writer.WaitForWritingCompleted;
    await store.Writer.WaitForIndexingCompleted;

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
    // Same ordering guarantee as DeleteContent: queued appends must land
    // (and index) before the clear, or they would resurrect after it.
    await store.Writer.WaitForWritingCompleted;
    await store.Writer.WaitForIndexingCompleted;

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

  internal static (byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize)
    AppendContent(this Store store, byte[] id, string collection, ReadOnlyMemory<byte>? content, ReadOnlyMemory<float>? embeddings)
  {
    (long contentPosition, long embeddingPosition, int dimensions, long size) actualindex = default;

    if (content?.Length == 0) content = null;
    if (embeddings?.Length == 0) embeddings = null;

    if (content == null || embeddings == null)
    {
      // Evaluate partial updates.
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

      // Evita embeddings.Value.ToArray(): scrive lo span come bytes
      embeddingsStream.WriteByteArray(embeddings.Value.Span);
      
      store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;
    }

    return (id, collection, contentPosition, embeddingPosition, embeddings?.Length ?? actualindex.dimensions, content?.Length ?? actualindex.size);
  }
}