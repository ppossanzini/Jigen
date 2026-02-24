using System.Buffers.Binary;
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
    if (!store.PositionIndex.ContainsKey(item.collectioname))
      store.PositionIndex[item.collectioname] = new Dictionary<byte[], (long, long, int, long)>(ByteArrayEqualityComparer.Instance);

    store.PositionIndex[item.collectioname][item.id] = (item.contentposition, item.embeddingposition, item.dimensions, item.contentsize);

    var file = store.IndexFileStream;

    file.Seek(0, SeekOrigin.End);
    file.WriteInt32Le(item.id.Length);
    file.Write(item.id, 0, item.id.Length);
    var nameAsBytes = Encoding.UTF8.GetBytes(item.collectioname);
    file.WriteInt32Le(nameAsBytes.Length);
    file.Write(nameAsBytes, 0, nameAsBytes.Length);
    file.WriteInt64Le(item.contentposition);
    file.WriteInt64Le(item.embeddingposition);
    file.WriteInt32Le(item.dimensions);
    file.WriteInt64Le(item.contentsize);
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

  internal static async Task<(byte[] id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize)>
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
      await contentStream.WriteAsync(id, 0, id.Length);
      contentStream.WriteInt32Le(content.Value.Length);

      // Scrive direttamente la ReadOnlyMemory<byte> senza ToArray()
      await contentStream.WriteAsync(content.Value);

      store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
    }

    var embeddingPosition = actualindex.embeddingPosition;
    if (embeddings != null)
    {
      var embeddingsStream = store.EmbeddingFileStream;
      embeddingsStream.Seek(0, SeekOrigin.End);
      embeddingPosition = embeddingsStream.Position;

      embeddingsStream.WriteInt32Le(id.Length);
      await embeddingsStream.WriteAsync(id, 0, id.Length);

      // Evita embeddings.Value.ToArray(): scrive lo span come bytes
      embeddingsStream.WriteByteArray(embeddings.Value.Span);

      store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;
    }

    return (id, collection, contentPosition, embeddingPosition, embeddings?.Length ?? actualindex.dimensions, content?.Length ?? actualindex.size);
  }
}