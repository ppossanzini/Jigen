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

    // Fixed-size part of a record after the two variable-length fields.
    const int FixedTailSize = 2 * sizeof(long) + sizeof(int) + sizeof(long);
    // Ids and collection names are keys: a length beyond this is torn/garbage
    // data, not a legitimate record (and would otherwise drive a huge alloc).
    const int MaxFieldLength = 64 * 1024;

    long fileLength = stream.Length;
    long recordStart = 0;
    var torn = false;

    stream.Seek(0, SeekOrigin.Begin);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    while (stream.Position < fileLength)
    {
      // A crash while appending can tear the last record: every length is
      // validated against what the file actually holds, and the first
      // inconsistent record marks the log tail as unreadable.
      recordStart = stream.Position;

      if (recordStart + sizeof(int) > fileLength) { torn = true; break; }
      var idsize = reader.ReadInt32();
      if (idsize <= 0 || idsize > MaxFieldLength || stream.Position + idsize + sizeof(int) > fileLength) { torn = true; break; }
      var id = reader.ReadBytes(idsize);

      var collectionNameLength = reader.ReadInt32();
      if (collectionNameLength <= 0 || collectionNameLength > MaxFieldLength ||
          stream.Position + collectionNameLength + FixedTailSize > fileLength) { torn = true; break; }
      var buffer = new Span<byte>(new byte[collectionNameLength]);
      stream.ReadExactly(buffer);
      var collectionName = Encoding.UTF8.GetString(buffer);
      var contentPosition = reader.ReadInt64();
      var embeddingsPosition = reader.ReadInt64();
      var dimensions = reader.ReadInt32();
      var size = reader.ReadInt64();

      // Positions must land inside their files (tombstones excepted):
      // out-of-range values mean the record bytes are garbage.
      if (!(contentPosition == IndexTombstone && embeddingsPosition == IndexTombstone) &&
          (contentPosition < 0 || contentPosition > ContentFileStream.Length ||
           embeddingsPosition < 0 || embeddingsPosition > EmbeddingFileStream.Length ||
           dimensions < 0 || size < 0)) { torn = true; break; }

      // Tombstone record: the entry was deleted after this point of the log.
      if (contentPosition == IndexTombstone && embeddingsPosition == IndexTombstone)
      {
        if (PositionIndex.TryGetValue(collectionName, out var collectionIndex) &&
            collectionIndex.TryRemove(id, out var removed))
        {
          if (removed.contentposition > 0)
            DeadContentBytes += ContentRecordSize(id.Length, removed.size);
          if (removed.embeddingsposition > 0)
            DeadEmbeddingBytes += EmbeddingRecordSize(id.Length, removed.dimensions);
        }
        continue;
      }

      if (!PositionIndex.TryGetValue(collectionName, out var index))
      {
        index = new ConcurrentDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance);
        PositionIndex[collectionName] = index;
      }

      // A record replacing an existing entry supersedes the old positions:
      // account them as dead space, like AppendIndex does at runtime.
      if (index.TryGetValue(id, out var old))
      {
        if (old.contentposition > 0 && old.contentposition != contentPosition)
          DeadContentBytes += ContentRecordSize(id.Length, old.size);
        if (old.embeddingsposition > 0 && old.embeddingsposition != embeddingsPosition)
          DeadEmbeddingBytes += EmbeddingRecordSize(id.Length, old.dimensions);
      }

      index[id] = (contentPosition, embeddingsPosition, dimensions, size);
    }

    if (torn)
    {
      // Everything from the first invalid byte on is unreadable: drop the tail
      // so the next append starts from a clean record boundary. The records
      // before it were already applied above.
      stream.SetLength(recordStart);
      stream.Flush(true);
    }

    // Collections fully emptied by tombstones would otherwise survive the
    // replay as empty dictionaries and reappear in GetCollections.
    foreach (var emptyCollection in PositionIndex.Where(kv => kv.Value.IsEmpty).Select(kv => kv.Key).ToList())
      PositionIndex.TryRemove(emptyCollection, out _);
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

  public float[] GetEmbedding(string collection, byte[] id)
  {
    if (!this.GetCollectionIndexOf(collection, out var index) || !index.TryGetValue(id,
          out (long contentposition, long embeddingposition, int dimensions, long size) item)) return null;

    if (item.embeddingposition <= 0 || item.dimensions <= 0) return null;

    // Record layout: [id length][id][dimensions * float] (see AppendContent).
    int headerSize = sizeof(int) + id.Length;
    byte[] rented = null;
    Span<byte> headerBuffer = headerSize <= 512
      ? stackalloc byte[headerSize]
      : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(headerSize)).AsSpan(0, headerSize);

    try
    {
      RandomAccess.Read(this.EmbeddingFileStream.SafeFileHandle, headerBuffer, item.embeddingposition);

      var idsize = BitConverter.ToInt32(headerBuffer.Slice(0, sizeof(int)));
      // A torn record (crash between the embedding write and the index flush)
      // must not abort callers like ReconcileIndexAsync: report it as absent.
      if (idsize != id.Length || !headerBuffer.Slice(sizeof(int), id.Length).SequenceEqual(id))
        return null;
    }
    finally
    {
      if (rented != null)
        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
    }

    var vector = new float[item.dimensions];
    RandomAccess.Read(this.EmbeddingFileStream.SafeFileHandle, MemoryMarshal.AsBytes(vector.AsSpan()), item.embeddingposition + sizeof(int) + id.Length);
    return vector;
  }
}