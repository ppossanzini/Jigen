using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
  private static void WriteInt32Le(FileStream stream, int value)
  {
    Span<byte> buf = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteInt64Le(FileStream stream, long value)
  {
    Span<byte> buf = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteByteArray(FileStream stream, ReadOnlySpan<float> embeddings)
  {
    stream.Write(MemoryMarshal.AsBytes(embeddings));
  }


  private static async Task AppendIndex(
    this Store store,
    (byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize) item)
  {
    if (!store.PositionIndex.ContainsKey(item.collectioname))
      store.PositionIndex[item.collectioname] = new Dictionary<byte[], (long, long, int, long)>(ByteArrayEqualityComparer.Instance);

    store.PositionIndex[item.collectioname][item.id] = (item.contentposition, item.embeddingposition, item.dimensions, item.contentsize);

    var file = store.IndexFileStream;

    file.Seek(0, SeekOrigin.End);
    WriteInt32Le(file, item.id.Length);
    file.Write(item.id, 0, item.id.Length);
    var nameAsBytes = Encoding.UTF8.GetBytes(item.collectioname);
    WriteInt32Le(file, nameAsBytes.Length);
    file.Write(nameAsBytes, 0, nameAsBytes.Length);
    WriteInt64Le(file, item.contentposition);
    WriteInt64Le(file, item.embeddingposition);
    WriteInt32Le(file, item.dimensions);
    WriteInt64Le(file, item.contentsize);

    await Task.CompletedTask;
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
    var result = store.PositionIndex.TryGetValue(collection, out var index) &&
                 index.Remove(key);

    if (result) await store.SaveIndexChanges();
    return result;
  }

  internal static async Task<(byte[] id, long position, long embeddingPosition, long size)>
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

      WriteInt32Le(contentStream, id.Length);
      await contentStream.WriteAsync(id, 0, id.Length);
      WriteInt32Le(contentStream, content.Value.Length);
      await contentStream.WriteAsync(content.Value.Span.ToArray(), 0, content.Value.Length);
      store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
    }

    var embeddingPosition = actualindex.embeddingPosition;
    if (embeddings != null)
    {
      var embeddingsStream = store.EmbeddingFileStream;
      embeddingsStream.Seek(0, SeekOrigin.End);
      embeddingPosition = embeddingsStream.Position;

      WriteInt32Le(embeddingsStream, id.Length);
      await embeddingsStream.WriteAsync(id, 0, id.Length);
      WriteByteArray(embeddingsStream, embeddings.Value.ToArray());

      store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;
    }

    await store.AppendIndex((id, collection, contentPosition, embeddingPosition, embeddings?.Length ?? actualindex.dimensions, content?.Length ?? actualindex.size));
    return (id, contentPosition, embeddingPosition, content?.Length ?? actualindex.size);
  }
}