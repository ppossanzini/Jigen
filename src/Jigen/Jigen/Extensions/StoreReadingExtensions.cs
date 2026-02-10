using System.Collections.Concurrent;
using System.Data;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreReadingExtensions
{
  internal static void ReadHeader(this Store store)
  {
    {
      var stream = store.EmbeddingFileStream;
      stream.Seek(0, SeekOrigin.End);
      store.VectorStoreHeader.EmbeddingCurrentPosition = stream.Position;
    }

    {
      var stream = store.ContentFileStream;
      stream.Seek(0, SeekOrigin.End);
      store.VectorStoreHeader.ContentCurrentPosition = stream.Position;
    }
  }

  internal static void LoadIndex(this Store store)
  {
    var stream = store.IndexFileStream;
    if (stream.Length == 0) return;

    const int EntrySize = sizeof(long) * 4;

    stream.Seek(0, SeekOrigin.Begin);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    while (stream.Position + EntrySize <= stream.Length)
    {
      var idsize = reader.ReadInt32();
      var id = reader.ReadBytes(idsize);
      var collectionNameLength = reader.ReadInt32();
      var buffer = new Span<byte>(new byte[collectionNameLength]);
      stream.ReadExactly(buffer);
      var collectionName = Encoding.UTF8.GetString(buffer);
      var contentPosition = reader.ReadInt64();
      var embeddingsPosition = reader.ReadInt64();
      var dimensions = reader.ReadInt32();
      var size = reader.ReadInt64();

      if (!store.PositionIndex.ContainsKey(collectionName))
        store.PositionIndex.Add(collectionName, new Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance));
      store.PositionIndex[collectionName][id] = (contentPosition, embeddingsPosition, dimensions, size);
    }
  }


  public static byte[] ReadContent(this Store store, string collection, byte[] id)
  {
    var accessor = store.ContentData.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
    if (!store.PositionIndex[collection].TryGetValue(id,
          out (long contentposition, long embeddingposition, int dimensions, long size) item)) return null;


    var idsize = accessor.ReadInt32(item.contentposition);
    var contentId = new byte[idsize];
    accessor.ReadArray<byte>(item.contentposition + sizeof(int), contentId, 0, idsize);

    if (!ByteArrayEqualityComparer.Instance.Equals(contentId, id)) throw new InvalidConstraintException("Content ID mismatch");

    byte[] buffer = new byte[item.size];
    accessor.ReadArray(item.contentposition + 2 * sizeof(int) + idsize, buffer, 0, (int)item.size);
    return buffer;
  }
}