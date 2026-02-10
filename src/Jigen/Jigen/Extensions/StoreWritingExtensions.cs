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

  internal static async Task<(byte[] id, long position, long embeddingPosition, long size)>
    AppendContent(this Store store, byte[] id, string collection, byte[] content, float[] embeddings)
  {
    var contentStream = store.ContentFileStream;

    contentStream.Seek(0, SeekOrigin.End);
    var currentPosition = contentStream.Position;

    WriteInt32Le(contentStream, id.Length);
    await contentStream.WriteAsync(id, 0, id.Length);
    WriteInt32Le(contentStream, content.Length);
    await contentStream.WriteAsync(content, 0, content.Length);

    store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;

    var embeddingsStream = store.EmbeddingFileStream;
    embeddingsStream.Seek(0, SeekOrigin.End);
    var embeddingPosition = embeddingsStream.Position;

    WriteInt32Le(embeddingsStream, id.Length);
    await embeddingsStream.WriteAsync(id, 0, id.Length);
    WriteByteArray(embeddingsStream, embeddings);

    store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;

    await store.AppendIndex((id, collection, currentPosition, embeddingPosition, embeddings.Length, content.Length));
    return (id, currentPosition, embeddingPosition, content.Length);
  }
}