using System.Buffers.Binary;
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
  private static void ReadExactly(Microsoft.Win32.SafeHandles.SafeFileHandle handle,
    Span<byte> destination, long fileOffset)
  {
    var read = 0;
    while (read < destination.Length)
    {
      var current = RandomAccess.Read(handle, destination[read..], fileOffset + read);
      if (current == 0)
        throw new EndOfStreamException($"Unexpected end of store file at offset {fileOffset + read}.");
      read += current;
    }
  }

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
    const int FixedTailSize = 2 * sizeof(long) + 2 * sizeof(int) + sizeof(long);
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
      var vectorCount = reader.ReadInt32();
      var size = reader.ReadInt64();

      // Validate the complete referenced records, not only their starting
      // positions. Plausible values in a torn index tail must not survive load
      // and trigger partial reads or oversized allocations later.
      var contentRecordSize = contentPosition > 0
        ? ContentRecordSize(idsize, size)
        : 0;
      var embeddingRecordSize = embeddingsPosition > 0
        ? EmbeddingRecordSize(idsize, dimensions, vectorCount)
        : 0;
      if (!(contentPosition == IndexTombstone && embeddingsPosition == IndexTombstone) &&
          (contentPosition < 0 || embeddingsPosition < 0 ||
           dimensions < 0 || vectorCount < 0 || size < 0 || size > int.MaxValue ||
           (contentPosition == 0 && size != 0) ||
           (embeddingsPosition == 0 && (dimensions != 0 || vectorCount != 0)) ||
           // From format version 2 a record may hold only its sparse section,
           // so an entry with no dense vectors is legitimate there.
           (embeddingsPosition > 0 && (WritesSparseSection
             ? dimensions < 0 || vectorCount < 0 || (dimensions == 0) != (vectorCount == 0)
             : dimensions <= 0 || vectorCount <= 0)) ||
           (contentPosition > 0 &&
            (contentRecordSize > ContentFileStream.Length ||
             contentPosition > ContentFileStream.Length - contentRecordSize)) ||
           (embeddingsPosition > 0 &&
            (embeddingRecordSize > EmbeddingFileStream.Length ||
             embeddingsPosition > EmbeddingFileStream.Length - embeddingRecordSize))))
      { torn = true; break; }

      // Tombstone record: the entry was deleted after this point of the log.
      if (contentPosition == IndexTombstone && embeddingsPosition == IndexTombstone)
      {
        if (PositionIndex.TryGetValue(collectionName, out var collectionIndex) &&
            collectionIndex.TryRemove(id, out var removed))
        {
          if (removed.contentposition > 0)
            DeadContentBytes += ContentRecordSize(id.Length, removed.size);
          if (removed.embeddingsposition > 0)
            DeadEmbeddingBytes += EmbeddingRecordSize(id.Length, removed.dimensions, removed.vectorcount);
        }
        continue;
      }

      if (!PositionIndex.TryGetValue(collectionName, out var index))
      {
        index = new ConcurrentDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, int vectorcount, long size)>(ByteArrayEqualityComparer.Instance);
        PositionIndex[collectionName] = index;
      }

      if (dimensions > 0)
      {
        var expectedDimensions = _collectionDimensions.GetOrAdd(collectionName, dimensions);
        if (expectedDimensions != dimensions) { torn = true; break; }
      }

      // A record replacing an existing entry supersedes the old positions:
      // account them as dead space, like AppendIndex does at runtime.
      if (index.TryGetValue(id, out var old))
      {
        if (old.contentposition > 0 && old.contentposition != contentPosition)
          DeadContentBytes += ContentRecordSize(id.Length, old.size);
        if (old.embeddingsposition > 0 && old.embeddingsposition != embeddingsPosition)
          DeadEmbeddingBytes += EmbeddingRecordSize(id.Length, old.dimensions, old.vectorcount);
      }

      index[id] = (contentPosition, embeddingsPosition, dimensions, vectorCount, size);
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
          out (long contentposition, long embeddingposition, int dimensions, int vectorcount, long size) item)) return null;

    int headerSize = 2 * sizeof(int) + id.Length;
    byte[] rented = null;
    Span<byte> headerBuffer = headerSize <= 512 
        ? stackalloc byte[headerSize] 
        : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(headerSize)).AsSpan(0, headerSize);

    try
    {
      ReadExactly(ContentFileStream.SafeFileHandle, headerBuffer, item.contentposition);
      
      var idsize = BitConverter.ToInt32(headerBuffer.Slice(0, sizeof(int)));
      if (idsize != id.Length || !headerBuffer.Slice(sizeof(int), id.Length).SequenceEqual(id))
        throw new InvalidConstraintException("Content ID mismatch");
      var contentSize = BitConverter.ToInt32(headerBuffer.Slice(sizeof(int) + id.Length, sizeof(int)));
      if (contentSize != item.size)
        throw new InvalidDataException("Content length does not match the index record.");
    }
    finally
    {
      if (rented != null)
        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
    }

    byte[] buffer = new byte[checked((int)item.size)];
    ReadExactly(ContentFileStream.SafeFileHandle, buffer, item.contentposition + 2 * sizeof(int) + id.Length);
    return buffer;
  }

  /// <summary>
  /// Every stored vector of a key (one per multi-vector slot), or null when the
  /// key does not exist or was stored without vectors.
  /// </summary>
  public float[][] GetEmbeddings(string collection, byte[] id)
  {
    if (!this.GetCollectionIndexOf(collection, out var index) || !index.TryGetValue(id,
          out (long contentposition, long embeddingposition, int dimensions, int vectorcount, long size) item)) return null;

    if (item.embeddingposition <= 0 || item.dimensions <= 0 || item.vectorcount <= 0) return null;

    // Record layout: [id length][id][vector count][vector count * dimensions * float] (see AppendContent).
    int headerSize = 2 * sizeof(int) + id.Length;
    byte[] rented = null;
    Span<byte> headerBuffer = headerSize <= 512
      ? stackalloc byte[headerSize]
      : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(headerSize)).AsSpan(0, headerSize);

    int vectorCount;
    try
    {
      ReadExactly(EmbeddingFileStream.SafeFileHandle, headerBuffer, item.embeddingposition);

      var idsize = BitConverter.ToInt32(headerBuffer.Slice(0, sizeof(int)));
      // A torn record (crash between the embedding write and the index flush)
      // must not abort callers like ReconcileIndexAsync: report it as absent.
      if (idsize != id.Length || !headerBuffer.Slice(sizeof(int), id.Length).SequenceEqual(id))
        return null;

      vectorCount = BitConverter.ToInt32(headerBuffer.Slice(sizeof(int) + id.Length, sizeof(int)));
      if (vectorCount != item.vectorcount)
        return null;
    }
    finally
    {
      if (rented != null)
        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
    }

    var flat = new float[checked(vectorCount * item.dimensions)];
    ReadExactly(EmbeddingFileStream.SafeFileHandle, MemoryMarshal.AsBytes(flat.AsSpan()),
      item.embeddingposition + 2 * sizeof(int) + id.Length);

    if (vectorCount == 1)
      return [flat];

    var vectors = new float[vectorCount][];
    for (var i = 0; i < vectorCount; i++)
    {
      vectors[i] = new float[item.dimensions];
      Array.Copy(flat, i * item.dimensions, vectors[i], 0, item.dimensions);
    }

    return vectors;
  }

  /// <summary>
  /// Every sparse vector of a key, in stored order, or null when the key does
  /// not exist, has no vector record, or the archive predates sparse vectors.
  /// Each vector holds its term/weight pairs ordered by ascending term.
  /// </summary>
  public IReadOnlyList<SparseTerm[]> GetSparseVectors(string collection, byte[] id)
  {
    if (!WritesSparseSection) return null!;
    if (!this.GetCollectionIndexOf(collection, out var index) || !index.TryGetValue(id,
          out (long contentposition, long embeddingposition, int dimensions, int vectorcount, long size) item)) return null!;
    if (item.embeddingposition <= 0) return null!;

    // Dense part: [id length][id][vector count][vector count * dimensions * float].
    var denseHeaderSize = 2 * sizeof(int) + id.Length;
    var sectionPosition = item.embeddingposition + denseHeaderSize
                          + (long)item.vectorcount * item.dimensions * sizeof(float);
    if (item.vectorcount == 0 || item.dimensions == 0)
      sectionPosition = item.embeddingposition + denseHeaderSize;

    Span<byte> countBuffer = stackalloc byte[sizeof(int)];
    ReadExactly(EmbeddingFileStream.SafeFileHandle, countBuffer, sectionPosition);
    var sparseVectorCount = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);
    if (sparseVectorCount is < 0 or > 65_536) return null!;
    if (sparseVectorCount == 0) return [];

    var counts = new int[sparseVectorCount];
    ReadExactly(EmbeddingFileStream.SafeFileHandle, MemoryMarshal.AsBytes(counts.AsSpan()),
      sectionPosition + sizeof(int));

    var totalPairs = 0L;
    foreach (var count in counts)
    {
      if (count is < 0 or > 16_777_216) return null!;
      totalPairs += count;
    }

    var pairs = new SparseTerm[totalPairs];
    ReadExactly(EmbeddingFileStream.SafeFileHandle, MemoryMarshal.AsBytes(pairs.AsSpan()),
      sectionPosition + sizeof(int) + (long)sparseVectorCount * sizeof(int));

    var vectors = new SparseTerm[sparseVectorCount][];
    var offset = 0;
    for (var vectorIndex = 0; vectorIndex < sparseVectorCount; vectorIndex++)
    {
      vectors[vectorIndex] = pairs.AsSpan(offset, counts[vectorIndex]).ToArray();
      offset += counts[vectorIndex];
    }

    return vectors;
  }
}
