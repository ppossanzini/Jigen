using System.Collections.Concurrent;
using System.Data;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Jigen.DataStructures;

namespace Jigen;

public partial class Store
{
  private void ReadHeader()
  {
    {
      var stream = this.EmbeddingFileStream;
      stream.Seek(0, SeekOrigin.End);
      VectorStoreHeader.EmbeddingCurrentPosition = stream.Position;
    }

    {
      var stream = this.ContentFileStream;
      stream.Seek(0, SeekOrigin.End);
      this.VectorStoreHeader.ContentCurrentPosition = stream.Position;
    }
  }

  private void LoadIndex()
  {
    var stream = this.IndexFileStream;
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

      if (!this.PositionIndex.ContainsKey(collectionName))
        this.PositionIndex.Add(collectionName, new Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance));
      this.PositionIndex[collectionName][id] = (contentPosition, embeddingsPosition, dimensions, size);
    }
  }

  public bool TryGetContent(string collection, byte[] id, out byte[] content)
  {
    content = this.GetContent(collection, id);
    return content is { Length: > 0 };
  }


  public byte[] GetContent(string collection, byte[] id)
  {
    if (!this.GetCollectionIndexOf(collection, out var index) || !index.TryGetValue(id,
          out (long contentposition, long embeddingposition, int dimensions, long size) item)) return null;

    var totalsize = this.ContentSize;
    using var accessor = this.GetContentAccessor(item.contentposition, Math.Min(totalsize - item.contentposition, item.size * 2 + 200));
    var idsize = accessor.ReadInt32(0);
    var contentId = new byte[idsize];
    accessor.ReadArray<byte>(sizeof(int), contentId, 0, idsize);

    if (!ByteArrayEqualityComparer.Instance.Equals(contentId, id)) throw new InvalidConstraintException("Content ID mismatch");

    byte[] buffer = new byte[item.size];
    accessor.ReadArray(2 * sizeof(int) + idsize, buffer, 0, (int)item.size);
    return buffer;
  }
}