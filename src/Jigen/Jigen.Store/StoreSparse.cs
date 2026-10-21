using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Jigen.DataStructures;

namespace Jigen;

/// <summary>
/// Sparse vector storage: a data file with the (term, weight) pairs of each
/// entry and a position log that maps a key to its record. Both are owned by
/// the store and written by the single writer thread, like the dense vectors;
/// the lexical index reads them and never owns the data.
/// Position 0 means "no sparse vector", so a collection without sparse vectors
/// never creates the files.
/// </summary>
public partial class Store
{
  private const long SparseHeaderSize = sizeof(long);
  private const long SparseIndexTombstone = -1;

  internal FileStream SparseFileStream;
  internal FileStream SparseIndexFileStream;

  /// <summary>Bytes made unreachable by deletes and overwrites of sparse records.</summary>
  internal long DeadSparseBytes;

  internal ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long position, int count)>> SparsePositionIndex { get; private set; }
    = new();

  internal static long SparseRecordSize(int idLength, int count)
    => 2L * sizeof(int) + idLength + (long)count * (sizeof(int) + sizeof(float));

  private static long SparseIndexRecordSize(int idLength, int collectionLength)
    => 2L * sizeof(int) + idLength + collectionLength + sizeof(long) + sizeof(int);

  internal string SparseFullFileName => ResolveDataFile($"{Options.DataBaseName}.sparse.jigen");
  internal string SparseIndexFullFileName => ResolveDataFile($"{Options.DataBaseName}.sparse.index.jigen");

  private bool SparseFilesExist => File.Exists(SparseFullFileName) || File.Exists(SparseIndexFullFileName);

  /// <summary>
  /// Opens the sparse files on first use. A database whose collections carry no
  /// sparse vectors never pays for two extra handles.
  /// </summary>
  internal void EnsureSparseFiles()
  {
    if (SparseFileStream is not null) return;

    var created = !File.Exists(SparseFullFileName);
    SparseFileStream = File.Open(SparseFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    if (created || SparseFileStream.Length == 0)
    {
      SparseFileStream.Write(BitConverter.GetBytes(SparseHeaderSize));
      SparseFileStream.Flush(true);
    }

    SparseIndexFileStream = File.Open(SparseIndexFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
  }

  /// <summary>
  /// Writes the sparse records of a writer batch and returns their positions in
  /// the same order as the input. Entries without a sparse vector are skipped;
  /// the returned position is 0 for them.
  /// </summary>
  internal List<(byte[] id, string collection, long position, int count)> AppendSparseBatch(List<VectorEntry> entries)
  {
    var results = new List<(byte[], string, long, int)>(entries.Count);
    if (entries.Count == 0) return results;

    var totalBytes = 0L;
    foreach (var entry in entries)
      if (entry.Sparse is not null)
        totalBytes += SparseRecordSize(entry.Id.Length, entry.Sparse.Count);

    if (totalBytes == 0)
    {
      foreach (var entry in entries)
        results.Add((entry.Id, entry.CollectionName, 0, 0));
      return results;
    }

    EnsureSparseFiles();

    byte[] rented = ArrayPool<byte>.Shared.Rent((int)totalBytes);
    var offset = 0;
    try
    {
      var basePosition = SparseFileStream.Seek(0, SeekOrigin.End);

      for (var i = 0; i < entries.Count; i++)
      {
        var entry = entries[i];
        if (entry.Sparse is null)
        {
          results.Add((entry.Id, entry.CollectionName, 0, 0));
          continue;
        }

        var position = basePosition + offset;
        var span = rented.AsSpan(offset);

        BinaryPrimitives.WriteInt32LittleEndian(span, entry.Id.Length);
        var p = sizeof(int);
        entry.Id.CopyTo(span[p..]);
        p += entry.Id.Length;

        BinaryPrimitives.WriteInt32LittleEndian(span[p..], entry.Sparse.Count);
        p += sizeof(int);

        var terms = entry.Sparse.TermsSpan;
        var weights = entry.Sparse.WeightsSpan;
        for (var t = 0; t < terms.Length; t++)
        {
          BinaryPrimitives.WriteInt32LittleEndian(span[p..], terms[t]);
          p += sizeof(int);
          BinaryPrimitives.WriteSingleLittleEndian(span[p..], weights[t]);
          p += sizeof(float);
        }

        offset += p;
        results.Add((entry.Id, entry.CollectionName, position, entry.Sparse.Count));
      }

      SparseFileStream.Write(rented, 0, offset);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(rented);
    }

    return results;
  }

  /// <summary>
  /// Records the sparse position of one entry and appends it to the position
  /// log. Called by the writer while it holds the I/O lock.
  /// </summary>
  internal void AppendSparseIndex((byte[] id, string collection, long position, int count) item)
  {
    if (item.position <= 0) return;

    lock (IndexAppendLock)
      WriteSparsePosition(item.id, item.collection, item.position, item.count);
  }

  private void WriteSparsePosition(byte[] id, string collection, long position, int count)
  {
    EnsureSparseFiles();

    var collectionIndex = SparsePositionIndex.GetOrAdd(collection,
      _ => new ConcurrentDictionary<byte[], (long, int)>(ByteArrayEqualityComparer.Instance));

    if (collectionIndex.TryGetValue(id, out var old) && old.position > 0)
      DeadSparseBytes += SparseRecordSize(id.Length, old.count);

    collectionIndex[id] = (position, count);

    WriteSparsePositionRecord(SparseIndexFileStream, id, collection, position, count);
  }

  private static void WriteSparsePositionRecord(FileStream file, byte[] id, string collection,
    long position, int count)
  {
    var nameByteCount = Encoding.UTF8.GetByteCount(collection);
    var size = (int)SparseIndexRecordSize(id.Length, nameByteCount);

    byte[] rented = null;
    Span<byte> buffer = size <= 512
      ? stackalloc byte[size]
      : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

    try
    {
      var p = 0;
      BinaryPrimitives.WriteInt32LittleEndian(buffer[p..], id.Length);
      p += sizeof(int);
      id.CopyTo(buffer[p..]);
      p += id.Length;

      BinaryPrimitives.WriteInt32LittleEndian(buffer[p..], nameByteCount);
      p += sizeof(int);
      Encoding.UTF8.GetBytes(collection, buffer[p..]);
      p += nameByteCount;

      BinaryPrimitives.WriteInt64LittleEndian(buffer[p..], position);
      p += sizeof(long);
      BinaryPrimitives.WriteInt32LittleEndian(buffer[p..], count);

      file.Seek(0, SeekOrigin.End);
      file.Write(buffer);
    }
    finally
    {
      if (rented != null) ArrayPool<byte>.Shared.Return(rented);
    }
  }

  /// <summary>
  /// Marks the sparse record of a key as deleted. Called under the index lock by
  /// the delete paths, which own the position they are retiring.
  /// </summary>
  internal void DeleteSparseCore(string collection, byte[] key)
  {
    if (!SparsePositionIndex.TryGetValue(collection, out var collectionIndex)) return;
    if (!collectionIndex.TryRemove(key, out var old)) return;

    if (old.position > 0)
      DeadSparseBytes += SparseRecordSize(key.Length, old.count);

    EnsureSparseFiles();
    WriteSparsePositionRecord(SparseIndexFileStream, key, collection, SparseIndexTombstone, 0);
  }

  /// <summary>Marks every sparse record of a collection as deleted.</summary>
  internal void ClearSparseCore(string collection)
  {
    if (!SparsePositionIndex.TryRemove(collection, out var collectionIndex)) return;

    EnsureSparseFiles();
    foreach (var (key, old) in collectionIndex)
    {
      if (old.position > 0)
        DeadSparseBytes += SparseRecordSize(key.Length, old.count);
      WriteSparsePositionRecord(SparseIndexFileStream, key, collection, SparseIndexTombstone, 0);
    }

    SparseIndexFileStream.Flush(false);
  }

  /// <summary>
  /// Reads the sparse vector of one entry, or null when the entry has none or
  /// the record is unreadable (a torn tail must not abort a caller).
  /// </summary>
  public SparseVector GetSparse(string collection, byte[] key)
  {
    if (!SparsePositionIndex.TryGetValue(collection, out var collectionIndex)) return null;
    if (!collectionIndex.TryGetValue(key, out var item) || item.position <= 0) return null;

    var stream = SparseFileStream;
    if (stream is null) return null;

    var headerSize = sizeof(int) + key.Length + sizeof(int);
    var header = new byte[headerSize];
    ReadExactly(stream.SafeFileHandle, header, item.position);

    var idLength = BinaryPrimitives.ReadInt32LittleEndian(header);
    if (idLength != key.Length || !header.AsSpan(sizeof(int), key.Length).SequenceEqual(key))
      return null;

    var count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(sizeof(int) + key.Length));
    if (count <= 0 || count > 1_000_000) return null;

    var pairs = new byte[(long)count * (sizeof(int) + sizeof(float))];
    if (pairs.LongLength > int.MaxValue) return null;
    ReadExactly(stream.SafeFileHandle, pairs, item.position + headerSize);

    var terms = new int[count];
    var weights = new float[count];
    var p = 0;
    for (var i = 0; i < count; i++)
    {
      terms[i] = BinaryPrimitives.ReadInt32LittleEndian(pairs.AsSpan(p));
      p += sizeof(int);
      weights[i] = BinaryPrimitives.ReadSingleLittleEndian(pairs.AsSpan(p));
      p += sizeof(float);
    }

    return SparseVector.Create(terms, weights, int.MaxValue);
  }

  /// <summary>
  /// Builds the sparse position index from its log. A torn tail is truncated,
  /// exactly like the dense index log: the records before it are already applied.
  /// </summary>
  private void LoadSparseIndex()
  {
    if (!SparseFilesExist) return;

    SparseFileStream = File.Open(SparseFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    SparseIndexFileStream = File.Open(SparseIndexFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

    var stream = SparseIndexFileStream;
    if (stream.Length == 0) return;

    var fileLength = stream.Length;
    var recordStart = 0L;
    var torn = false;
    stream.Seek(0, SeekOrigin.Begin);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

    while (stream.Position < fileLength)
    {
      recordStart = stream.Position;

      if (recordStart + sizeof(int) > fileLength) { torn = true; break; }
      var idLength = reader.ReadInt32();
      if (idLength <= 0 || idLength > 64 * 1024 || stream.Position + idLength + sizeof(int) > fileLength)
      { torn = true; break; }
      var id = reader.ReadBytes(idLength);

      var collectionLength = reader.ReadInt32();
      if (collectionLength <= 0 || collectionLength > 64 * 1024
          || stream.Position + collectionLength + sizeof(long) + sizeof(int) > fileLength)
      { torn = true; break; }
      var collectionBytes = new byte[collectionLength];
      stream.ReadExactly(collectionBytes);
      var collection = Encoding.UTF8.GetString(collectionBytes);

      var position = reader.ReadInt64();
      var count = reader.ReadInt32();

      if (position == SparseIndexTombstone)
      {
        if (SparsePositionIndex.TryGetValue(collection, out var collectionIndex))
          collectionIndex.TryRemove(id, out _);
        continue;
      }

      if (position < 0 || count < 0
          || position + SparseRecordSize(idLength, count) > SparseFileStream.Length)
      { torn = true; break; }

      var target = SparsePositionIndex.GetOrAdd(collection,
        _ => new ConcurrentDictionary<byte[], (long, int)>(ByteArrayEqualityComparer.Instance));
      target[id] = (position, count);
    }

    if (torn)
    {
      stream.SetLength(recordStart);
      stream.Flush(true);
    }
  }

  /// <summary>
  /// Rebuilds the sparse position index by scanning the data file, keeping the
  /// last record of each key and dropping keys the store no longer holds. Used
  /// after an unclean shutdown: the data file is the archive, the log is derived.
  /// </summary>
  internal void ReconcileSparse()
  {
    if (!SparseFilesExist) return;
    EnsureSparseFiles();

    var rebuilt = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long, int)>>();
    var stream = SparseFileStream;
    var fileLength = stream.Length;

    // One pass over the live positions: a record whose key the store no longer
    // holds belongs to a deleted entry and must not come back.
    var liveCollections = new Dictionary<byte[], string>(ByteArrayEqualityComparer.Instance);
    foreach (var (collection, index) in PositionIndex)
      foreach (var key in index.Keys)
        liveCollections[key] = collection;

    if (fileLength > SparseHeaderSize)
    {
      var recordPosition = SparseHeaderSize;
      while (recordPosition < fileLength)
      {
        var headerSize = sizeof(int);
        if (recordPosition + headerSize > fileLength) break;

        var header = new byte[headerSize];
        ReadExactly(stream.SafeFileHandle, header, recordPosition);
        var idLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (idLength <= 0 || idLength > 64 * 1024) break;

        var idAndCount = new byte[headerSize + idLength + sizeof(int)];
        if (recordPosition + idAndCount.LongLength > fileLength) break;
        ReadExactly(stream.SafeFileHandle, idAndCount, recordPosition);

        var id = idAndCount.AsSpan(sizeof(int), idLength).ToArray();
        var count = BinaryPrimitives.ReadInt32LittleEndian(idAndCount.AsSpan(sizeof(int) + idLength));
        if (count < 0) break;

        var recordSize = SparseRecordSize(idLength, count);
        if (recordPosition + recordSize > fileLength) break;

        // A key present in two records is an overwrite: the later record wins.
        if (liveCollections.TryGetValue(id, out var collection))
        {
          var target = rebuilt.GetOrAdd(collection,
            _ => new ConcurrentDictionary<byte[], (long, int)>(ByteArrayEqualityComparer.Instance));
          target[id] = (recordPosition, count);
        }

        recordPosition += recordSize;
      }
    }

    SparsePositionIndex = rebuilt;
  }

  /// <summary>Appends the sparse position log entries for a reconciled dictionary.</summary>
  internal void RewriteSparsePositionLog()
  {
    EnsureSparseFiles();
    SparseIndexFileStream.SetLength(0);
    foreach (var (collection, index) in SparsePositionIndex)
      foreach (var (key, item) in index)
        WriteSparsePositionRecord(SparseIndexFileStream, key, collection, item.position, item.count);
    SparseIndexFileStream.Flush(true);
  }

  /// <summary>Bytes held by the sparse data file, zero when it was never created.</summary>
  internal long SparseFileLength => SparseFileStream?.Length ?? 0;
}
