using Jigen.DataStructures;
using Jigen.Extensions;


namespace Jigen;

/// <summary>
/// A multi-entry transaction. Operations are buffered in memory and become
/// atomically durable when <see cref="CommitAsync"/> is called: the entire
/// transaction is serialized as a single WAL block [BEGIN][ops...][COMMIT].
///
/// On recovery, transactions without a COMMIT marker are rolled back
/// (the WAL records are skipped).
///
/// Usage:
/// <code>
/// using var tx = store.BeginTransaction();
/// tx.Append(new VectorEntry { Id = key1, CollectionName = "docs", ... });
/// tx.Delete("docs", key2);
/// await tx.CommitAsync();  // or Dispose calls Rollback if not committed
/// </code>
/// </summary>
public sealed class Transaction : IDisposable, IAsyncDisposable
{
  private readonly Store _store;
  private readonly Guid _txId;
  private readonly List<VectorEntry> _pending = new();
  private readonly List<(byte[] Key, string Collection)> _pendingDeletes = new();
  private bool _committed;
  private bool _disposed;

  /// <summary>The unique identifier for this transaction.</summary>
  public Guid Id => _txId;

  internal Transaction(Store store)
  {
    _store = store;
    _txId = Guid.NewGuid();
  }

  /// <summary>
  /// Buffers an insert/upsert entry. Not visible to readers until <see cref="CommitAsync"/>.
  /// </summary>
  public void Append(VectorEntry entry)
  {
    ThrowIfClosed();
    _pending.Add(entry);
  }

  /// <summary>
  /// Buffers a delete. Not visible to readers until <see cref="CommitAsync"/>.
  /// </summary>
  public void Delete(string collection, byte[] key)
  {
    ThrowIfClosed();
    _pendingDeletes.Add((key, collection));
  }

  /// <summary>
  /// Serializes the entire transaction to the WAL and enqueues the operations
  /// for background processing (data files + index).
  /// After this returns, the transaction is durable.
  /// </summary>
  public async Task CommitAsync()
  {
    ThrowIfClosed();
    if (_committed)
      throw new InvalidOperationException("Transaction already committed.");
    _committed = true;

    if (_pending.Count == 0 && _pendingDeletes.Count == 0)
      return; // nothing to commit

    // WAL must be enabled for atomic transactions.
    if (_store.Options.Wal?.Enabled != true)
      throw new InvalidOperationException(
        "Transactions require the Write-Ahead Log to be enabled. Set StoreOptions.Wal.Enabled = true.");

    // 1. Calculate WAL buffer size.
    int totalWalSize = WalRecord.BeginTransactionSize + WalRecord.CommitTransactionSize;
    foreach (var e in _pending)
    {
      totalWalSize += WalRecord.InsertRecordSize(
        e.Id, e.CollectionName,
        e.Content.IsEmpty ? null : e.Content.ToArray(),
        e.Embedding.IsEmpty ? null : e.Embedding.ToArray());
    }
    foreach (var (key, collection) in _pendingDeletes)
      totalWalSize += WalRecord.DeleteRecordSize(key, collection);

    // 2. Serialize: [BEGIN][inserts...][deletes...][COMMIT]
    byte[] walBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(totalWalSize);
    try
    {
      int pos = 0;
      pos += WalRecord.SerializeBeginTransaction(walBuffer.AsSpan(pos), _txId);

      foreach (var e in _pending)
        pos += SerializeInsert(walBuffer.AsSpan(pos), e);

      foreach (var (key, collection) in _pendingDeletes)
        pos += WalRecord.SerializeDelete(walBuffer.AsSpan(pos), key, collection);

      pos += WalRecord.SerializeCommitTransaction(walBuffer.AsSpan(pos), _txId);

      // 3. Atomic write: the whole transaction is one Write call.
      _store.WalFileStream!.Write(walBuffer, 0, pos);

      // 4. fsync based on configured durability.
      switch (_store.Options.Wal.Durability)
      {
        case WalDurability.PerWrite:
          _store.WalFileStream.Flush(true);
          break;
        case WalDurability.Group:
          _store.WalFileStream.Flush(false); // group commit handles fsync
          break;
        default:
          _store.WalFileStream.Flush(false);
          break;
      }
    }
    finally
    {
      System.Buffers.ArrayPool<byte>.Shared.Return(walBuffer);
    }

    // 5. Enqueue all entries into the IngestionQueue for background processing.
    //    The Writer thread handles content/vectors/index writes + HNSW indexing.
    foreach (var entry in _pending)
      _store.IngestionQueue.Enqueue(entry);

    foreach (var (key, collection) in _pendingDeletes)
    {
      // Deletes go through the same delete path: drain pipeline, remove from
      // PositionIndex, write tombstone, signal indexer.
      await _store.DeleteContent(collection, key);
    }

    _store.Writer.SignalNewData();
  }

  /// <summary>
  /// Discards all buffered operations without writing anything to the WAL.
  /// </summary>
  public void Rollback()
  {
    if (_disposed) return;
    _committed = true; // prevent double-commit
    _pending.Clear();
    _pendingDeletes.Clear();
  }

  private void ThrowIfClosed()
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(Transaction));
    if (_committed)
      throw new InvalidOperationException("Transaction already committed or rolled back.");
  }

  private static int SerializeInsert(Span<byte> buffer, VectorEntry entry)
  {
    var content = entry.Content.IsEmpty ? null : entry.Content.ToArray();
    var embedding = entry.Embedding.IsEmpty ? null : entry.Embedding.ToArray();
    return WalRecord.SerializeInsert(buffer, entry.Id, entry.CollectionName, content, embedding);
  }

  /// <summary>Rolls back if not yet committed.</summary>
  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    Rollback();
  }

  /// <summary>Rolls back if not yet committed.</summary>
  public ValueTask DisposeAsync()
  {
    Dispose();
    return ValueTask.CompletedTask;
  }
}
