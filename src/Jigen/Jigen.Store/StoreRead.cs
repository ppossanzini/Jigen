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

      if (!PositionIndex.TryGetValue(collectionName, out var index))
      {
        index = new Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance);
        PositionIndex.Add(collectionName, index);
      }
      index[id] = (contentPosition, embeddingsPosition, dimensions, size);
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

    int headerSize = sizeof(int) + id.Length;
    byte[] rented = null;
    Span<byte> headerBuffer = headerSize <= 512 
        ? stackalloc byte[headerSize] 
        : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(headerSize)).AsSpan(0, headerSize);

    try
    {
      RandomAccess.Read(this.ContentFileStream.SafeFileHandle, headerBuffer, item.contentposition);
      
      var idsize = BitConverter.ToInt32(headerBuffer.Slice(0, sizeof(int)));
      if (idsize != id.Length || !headerBuffer.Slice(sizeof(int), id.Length).SequenceEqual(id))
        throw new InvalidConstraintException("Content ID mismatch");
    }
    finally
    {
      if (rented != null)
        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
    }

    byte[] buffer = new byte[item.size];
    RandomAccess.Read(this.ContentFileStream.SafeFileHandle, buffer, item.contentposition + 2 * sizeof(int) + id.Length);
    return buffer;
  }
}