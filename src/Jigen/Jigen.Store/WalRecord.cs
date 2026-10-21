using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;



namespace Jigen;

/// <summary>
/// On-disk record types stored in the WAL file.
/// </summary>
public enum WalRecordType : byte
{
  Insert = 0x01,
  Delete = 0x02,
  ClearCollection = 0x03,
  Checkpoint = 0xFE,
  BeginTransaction = 0x05,
  CommitTransaction = 0x06,
}

/// <summary>
/// Immutable WAL record structures and low-level serialization / deserialization.
/// All methods are static and operate on spans — the WAL is a single sequential
/// file with one writer (the caller thread, not the background WriterThread).
/// </summary>
public static class WalRecord
{
  private static readonly uint[] CrcTable = BuildCrcTable();

  private static uint[] BuildCrcTable()
  {
    var table = new uint[256];
    for (uint i = 0; i < 256; i++)
    {
      var crc = i;
      for (int j = 0; j < 8; j++)
        crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
      table[i] = crc;
    }
    return table;
  }

  public static uint ComputeCrc32(ReadOnlySpan<byte> data)
  {
    var crc = 0xFFFFFFFFu;
    for (int i = 0; i < data.Length; i++)
      crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
    return ~crc;
  }

  private const int LengthFieldSize = sizeof(int);
  private const int TypeFieldSize = sizeof(byte);
  private const int CrcFieldSize = sizeof(uint);

  public const int HeaderSize = CrcFieldSize + LengthFieldSize + TypeFieldSize; // 9

  private static int VarFieldSize(int payloadLength) =>
    sizeof(int) + payloadLength;

  private static int IdFieldSize(byte[] id) => VarFieldSize(id.Length);

  private static readonly UTF8Encoding Utf8NoBom = new(false, false);

  private static int CollectionFieldSize(string collection)
  {
    var nameByteCount = Utf8NoBom.GetByteCount(collection);
    return VarFieldSize(nameByteCount);
  }

  // ── Sizing ──

  public static int InsertRecordSize(byte[] id, string collection, byte[] content, float[] embedding)
    => InsertRecordSize(id, collection, content, embedding, null);

  /// <summary>
  /// Size of an insert record. The sparse section is written only when a sparse
  /// vector is present, so a record without one keeps the legacy layout and
  /// stays readable by the previous format.
  /// </summary>
  public static int InsertRecordSize(byte[] id, string collection, byte[] content, float[] embedding,
    SparseVector sparse)
  {
    var payload = LengthFieldSize + TypeFieldSize
                  + IdFieldSize(id)
                  + CollectionFieldSize(collection)
                  + ContentFieldSize(content ?? [])
                  + EmbeddingFieldSize(embedding ?? [])
                  + SparseFieldSize(sparse);
    return CrcFieldSize + payload;
  }

  public static int DeleteRecordSize(byte[] id, string collection)
  {
    var payload = LengthFieldSize + TypeFieldSize
                  + IdFieldSize(id)
                  + CollectionFieldSize(collection);
    return CrcFieldSize + payload;
  }

  public static int ClearCollectionRecordSize(string collection)
  {
    var payload = LengthFieldSize + TypeFieldSize
                  + VarFieldSize(0)               // empty id
                  + CollectionFieldSize(collection);
    return CrcFieldSize + payload;
  }

  public const int CheckpointMarkerSize = CrcFieldSize + LengthFieldSize + TypeFieldSize;

  // Transaction markers: [CRC(4)][Length(4)][Type(1)][TxId(16)]
  private const int TxIdFieldSize = 16; // Guid as 16 bytes
  public const int BeginTransactionSize = CrcFieldSize + LengthFieldSize + TypeFieldSize + TxIdFieldSize;
  public const int CommitTransactionSize = CrcFieldSize + LengthFieldSize + TypeFieldSize + TxIdFieldSize;

  private static int ContentFieldSize(byte[] content) =>
    content is { Length: > 0 } ? VarFieldSize(content.Length) : VarFieldSize(0);

  private static int EmbeddingFieldSize(float[] embedding) =>
    embedding is { Length: > 0 } ? sizeof(int) + embedding.Length * sizeof(float)
                                : VarFieldSize(0);

  private static int SparseFieldSize(SparseVector sparse) =>
    sparse is null ? 0 : sizeof(int) + sparse.Count * (sizeof(int) + sizeof(float));

  // ── Serialization ──

  /// <summary>
  /// Serializes an insert record. Returns bytes written.
  /// Layout: [CRC(4)][Length(4)][Type:0x01][id][collection][content][embedding]
  /// </summary>
  public static int SerializeInsert(Span<byte> buffer, byte[] id, string collection, byte[] content, float[] embedding)
    => SerializeInsert(buffer, id, collection, content, embedding, null);

  /// <summary>
  /// Serializes an insert record including the optional sparse section.
  /// Layout: [CRC][Length][Type][id][collection][content][embedding][sparse]
  /// where sparse is [count][(term)(weight)]*count, written only when present.
  /// </summary>
  public static int SerializeInsert(Span<byte> buffer, byte[] id, string collection, byte[] content,
    float[] embedding, SparseVector sparse)
  {
    int totalSize = InsertRecordSize(id, collection, content, embedding, sparse);
    int payloadOffset = CrcFieldSize + LengthFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.Insert;
    int pos = payloadOffset + TypeFieldSize;

    // ID
    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], id.Length);
    pos += sizeof(int);
    id.CopyTo(buffer[pos..]);
    pos += id.Length;

    // Collection
    var nameByteCount = Utf8NoBom.GetByteCount(collection);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], nameByteCount);
    pos += sizeof(int);
    Utf8NoBom.GetBytes(collection, buffer[pos..]);
    pos += nameByteCount;

    // Content
    if (content is { Length: > 0 })
    {
      BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], content.Length);
      pos += sizeof(int);
      content.CopyTo(buffer[pos..]);
      pos += content.Length;
    }
    else
    {
      BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], 0);
      pos += sizeof(int);
    }

    // Embedding
    if (embedding is { Length: > 0 })
    {
      BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], embedding.Length);
      pos += sizeof(int);
      MemoryMarshal.AsBytes<float>(embedding).CopyTo(buffer[pos..]);
      pos += embedding.Length * sizeof(float);
    }
    else
    {
      BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], 0);
      pos += sizeof(int);
    }

    // Sparse section: absent means no bytes at all, so the previous format
    // (which ends after the embedding) remains readable by an older reader.
    if (sparse is not null)
    {
      BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], sparse.Count);
      pos += sizeof(int);
      var terms = sparse.TermsSpan;
      var weights = sparse.WeightsSpan;
      for (var i = 0; i < terms.Length; i++)
      {
        BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], terms[i]);
        pos += sizeof(int);
        BinaryPrimitives.WriteSingleLittleEndian(buffer[pos..], weights[i]);
        pos += sizeof(float);
      }
    }

    Debug.Assert(pos == totalSize, "Serialization size mismatch");

    var payloadSize = totalSize - CrcFieldSize - LengthFieldSize;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes an insert record from a VectorEntry. Returns bytes written.
  /// </summary>
  public static int SerializeInsert(Span<byte> buffer, VectorEntry entry)
  {
    var content = entry.Content.IsEmpty ? null : entry.Content.ToArray();
    var embedding = entry.Embedding.IsEmpty ? null : entry.Embedding.ToArray();
    return SerializeInsert(buffer, entry.Id, entry.CollectionName, content, embedding, entry.Sparse);
  }

  /// <summary>
  /// Serializes a delete tombstone record. Returns bytes written.
  /// </summary>
  public static int SerializeDelete(Span<byte> buffer, byte[] id, string collection)
  {
    int totalSize = DeleteRecordSize(id, collection);
    int payloadOffset = CrcFieldSize + LengthFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.Delete;
    int pos = payloadOffset + TypeFieldSize;

    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], id.Length);
    pos += sizeof(int);
    id.CopyTo(buffer[pos..]);
    pos += id.Length;

    var nameByteCount = Utf8NoBom.GetByteCount(collection);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], nameByteCount);
    pos += sizeof(int);
    Utf8NoBom.GetBytes(collection, buffer[pos..]);
    pos += nameByteCount;

    var payloadSize = totalSize - CrcFieldSize - LengthFieldSize;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes a clear-collection record. Returns bytes written.
  /// </summary>
  public static int SerializeClearCollection(Span<byte> buffer, string collection)
  {
    int totalSize = ClearCollectionRecordSize(collection);
    int payloadOffset = CrcFieldSize + LengthFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.ClearCollection;
    int pos = payloadOffset + TypeFieldSize;

    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], 0); // empty id
    pos += sizeof(int);

    var nameByteCount = Utf8NoBom.GetByteCount(collection);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[pos..], nameByteCount);
    pos += sizeof(int);
    Utf8NoBom.GetBytes(collection, buffer[pos..]);
    pos += nameByteCount;

    var payloadSize = totalSize - CrcFieldSize - LengthFieldSize;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes a checkpoint marker (type=0xFE, no payload). Returns bytes written.
  /// </summary>
  public static int SerializeCheckpoint(Span<byte> buffer)
  {
    int totalSize = CheckpointMarkerSize;
    int payloadOffset = CrcFieldSize + LengthFieldSize;
    const int payloadSize = TypeFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.Checkpoint;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes a BeginTransaction marker. Returns bytes written.
  /// Layout: [CRC(4)][Length(4)][Type:0x05][TxId(16)]
  /// </summary>
  public static int SerializeBeginTransaction(Span<byte> buffer, Guid txId)
  {
    int totalSize = BeginTransactionSize;
    int payloadOffset = CrcFieldSize + LengthFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.BeginTransaction;
    int pos = payloadOffset + TypeFieldSize;

    txId.TryWriteBytes(buffer[pos..]);
    pos += TxIdFieldSize;

    var payloadSize = totalSize - CrcFieldSize - LengthFieldSize;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes a CommitTransaction marker. Returns bytes written.
  /// Layout: [CRC(4)][Length(4)][Type:0x06][TxId(16)]
  /// </summary>
  public static int SerializeCommitTransaction(Span<byte> buffer, Guid txId)
  {
    int totalSize = CommitTransactionSize;
    int payloadOffset = CrcFieldSize + LengthFieldSize;

    buffer[payloadOffset] = (byte)WalRecordType.CommitTransaction;
    int pos = payloadOffset + TypeFieldSize;

    txId.TryWriteBytes(buffer[pos..]);
    pos += TxIdFieldSize;

    var payloadSize = totalSize - CrcFieldSize - LengthFieldSize;
    BinaryPrimitives.WriteInt32LittleEndian(buffer[CrcFieldSize..], payloadSize);
    var crc = ComputeCrc32(buffer[CrcFieldSize..totalSize]);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

    return totalSize;
  }

  /// <summary>
  /// Serializes a batch of Insert records into a pre-sized buffer.
  /// Returns the total bytes written.
  /// </summary>
  public static int SerializeInsertBatch(VectorEntry[] entries, Span<byte> buffer)
  {
    int pos = 0;
    foreach (var entry in entries)
    {
      var content = entry.Content.IsEmpty ? null : entry.Content.ToArray();
      var embedding = entry.Embedding.IsEmpty ? null : entry.Embedding.ToArray();
      pos += SerializeInsert(buffer[pos..], entry.Id, entry.CollectionName, content, embedding);
    }
    return pos;
  }

  // ── Deserialization ──

  /// <summary>
  /// Reads one WAL record from the stream. Returns false on end-of-stream
  /// or CRC mismatch (torn write).
  /// </summary>
  public static bool TryReadRecord(
    Stream stream,
    out WalRecordType type,
    out byte[] id,
    out string collection,
    out byte[] content,
    out float[] embedding,
    out int bytesRead)
  {
    return TryReadRecord(stream, out type, out id, out collection, out content, out embedding,
      out _, out _, out bytesRead);
  }

  /// <summary>
  /// Reads one WAL record from the stream, including transaction markers.
  /// Returns false on end-of-stream or CRC mismatch (torn write).
  /// <paramref name="txId"/> is set only for <see cref="WalRecordType.BeginTransaction"/>
  /// and <see cref="WalRecordType.CommitTransaction"/>.
  /// </summary>
  public static bool TryReadRecord(
    Stream stream,
    out WalRecordType type,
    out byte[] id,
    out string collection,
    out byte[] content,
    out float[] embedding,
    out Guid txId,
    out int bytesRead)
  {
    return TryReadRecord(stream, out type, out id, out collection, out content, out embedding,
      out _, out txId, out bytesRead);
  }

  /// <summary>
  /// Reads one WAL record, including the optional sparse section of an insert.
  /// A record written without that section yields <paramref name="sparse"/> null:
  /// the section is recognised by the bytes still available after the embedding,
  /// so records produced before the sparse slot existed stay readable.
  /// </summary>
  public static bool TryReadRecord(
    Stream stream,
    out WalRecordType type,
    out byte[] id,
    out string collection,
    out byte[] content,
    out float[] embedding,
    out SparseVector sparse,
    out Guid txId,
    out int bytesRead)
  {
    type = 0;
    id = [];
    collection = string.Empty;
    content = [];
    embedding = [];
    sparse = null;
    txId = Guid.Empty;
    bytesRead = 0;

    Span<byte> header = stackalloc byte[HeaderSize];
    int read = stream.Read(header);
    if (read == 0) return false;
    if (read < HeaderSize) return false;

    var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(header);
    var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header[CrcFieldSize..]);
    var recordType = (WalRecordType)header[CrcFieldSize + LengthFieldSize];

    const int maxPayload = 128 * 1024 * 1024;
    if (payloadLength < TypeFieldSize || payloadLength > maxPayload)
      return false;

    int totalSize = CrcFieldSize + LengthFieldSize + payloadLength;
    int remainingPayload = payloadLength - TypeFieldSize;

    byte[] rentedPayload = null;
    Span<byte> payloadBuffer = remainingPayload <= 4096
      ? stackalloc byte[remainingPayload]
      : (rentedPayload = ArrayPool<byte>.Shared.Rent(remainingPayload)).AsSpan(0, remainingPayload);

    try
    {
      read = stream.Read(payloadBuffer);
      if (read < remainingPayload) return false;

      // Verify CRC
      int verifyLen = LengthFieldSize + TypeFieldSize + remainingPayload;
      byte[] rentedVerify = null;
      Span<byte> verifyBuffer = verifyLen <= 4096
        ? stackalloc byte[verifyLen]
        : (rentedVerify = ArrayPool<byte>.Shared.Rent(verifyLen)).AsSpan(0, verifyLen);

      try
      {
        header.Slice(CrcFieldSize, LengthFieldSize + TypeFieldSize).CopyTo(verifyBuffer);
        payloadBuffer.CopyTo(verifyBuffer[(LengthFieldSize + TypeFieldSize)..]);
        if (ComputeCrc32(verifyBuffer) != storedCrc) return false;
      }
      finally
      {
        if (rentedVerify != null) ArrayPool<byte>.Shared.Return(rentedVerify);
      }

      type = recordType;
      int p = 0;

      if (type == WalRecordType.Checkpoint)
      {
        bytesRead = totalSize;
        return true;
      }

      // Transaction markers: payload is just the TxId (16 bytes)
      if (type == WalRecordType.BeginTransaction || type == WalRecordType.CommitTransaction)
      {
        if (remainingPayload < TxIdFieldSize) return false;
        txId = new Guid(payloadBuffer[..TxIdFieldSize]);
        bytesRead = totalSize;
        return true;
      }

      // Read ID
      if (p + sizeof(int) > remainingPayload) return false;
      var idLength = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
      p += sizeof(int);
      if (idLength < 0 || idLength > 64 * 1024) return false;
      if (idLength == 0)
      {
        id = [];
      }
      else
      {
        if (p + idLength > remainingPayload) return false;
        id = payloadBuffer.Slice(p, idLength).ToArray();
        p += idLength;
      }

      // Read Collection
      if (p + sizeof(int) > remainingPayload) return false;
      var collectionLen = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
      p += sizeof(int);
      if (collectionLen < 0 || collectionLen > 64 * 1024) return false;
      if (collectionLen == 0)
      {
        collection = string.Empty;
      }
      else
      {
        if (p + collectionLen > remainingPayload) return false;
        collection = Utf8NoBom.GetString(payloadBuffer.Slice(p, collectionLen));
        p += collectionLen;
      }

      // Insert records have content + embedding
      if (type == WalRecordType.Insert)
      {
        // Read Content
        if (p + sizeof(int) > remainingPayload) return false;
        var contentLen = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
        p += sizeof(int);
        if (contentLen < 0 || contentLen > 128 * 1024 * 1024) return false;
        if (contentLen == 0)
        {
          content = [];
        }
        else
        {
          if (p + contentLen > remainingPayload) return false;
          content = payloadBuffer.Slice(p, contentLen).ToArray();
          p += contentLen;
        }

        // Read Embedding
        if (p + sizeof(int) > remainingPayload) return false;
        var dimensions = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
        p += sizeof(int);
        if (dimensions < 0 || dimensions > 1_000_000) return false;
        if (dimensions == 0)
        {
          embedding = [];
        }
        else
        {
          int embeddingBytes = dimensions * sizeof(float);
          if (p + embeddingBytes > remainingPayload) return false;
          // Copy the payload bytes INTO the vector: the previous direction
          // copied the freshly allocated zeros onto the buffer, so every
          // replayed entry came back with a zeroed embedding.
          embedding = new float[dimensions];
          payloadBuffer.Slice(p, embeddingBytes)
            .CopyTo(MemoryMarshal.AsBytes(embedding.AsSpan()));
          p += embeddingBytes;
        }

        // Optional sparse section: absent in records written before the slot
        // existed, which end exactly here.
        if (p + sizeof(int) <= remainingPayload)
        {
          var termCount = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
          p += sizeof(int);
          if (termCount < 0 || termCount > 1_000_000) return false;
          if (termCount > 0)
          {
            var pairBytes = termCount * (sizeof(int) + sizeof(float));
            if (p + pairBytes > remainingPayload) return false;

            var terms = new int[termCount];
            var weights = new float[termCount];
            for (var i = 0; i < termCount; i++)
            {
              terms[i] = BinaryPrimitives.ReadInt32LittleEndian(payloadBuffer[p..]);
              p += sizeof(int);
              weights[i] = BinaryPrimitives.ReadSingleLittleEndian(payloadBuffer[p..]);
              p += sizeof(float);
            }

            // A corrupted section must not resurrect an invalid vector: the
            // record is reported as unreadable instead of half-applied.
            try
            {
              sparse = SparseVector.Create(terms, weights, int.MaxValue);
            }
            catch (ArgumentException)
            {
              return false;
            }
          }
        }
      }

      bytesRead = totalSize;
      return true;
    }
    finally
    {
      if (rentedPayload != null) ArrayPool<byte>.Shared.Return(rentedPayload);
    }
  }
}
