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
  private enum OperationType { Insert, Delete }
  private sealed record Operation(OperationType Type, VectorEntry Entry, byte[] Key, string Collection);
  private readonly List<Operation> _operations = new();
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
    _store.ValidateVectorDimensions(entry);
    _operations.Add(new Operation(OperationType.Insert, entry, null, null));
  }

  /// <summary>
  /// Buffers a delete. Not visible to readers until <see cref="CommitAsync"/>.
  /// </summary>
  public void Delete(string collection, byte[] key)
  {
    ThrowIfClosed();
    _operations.Add(new Operation(OperationType.Delete, null, key, collection));
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
    if (_operations.Count == 0)
    {
      _committed = true;
      return; // nothing to commit
    }

    // WAL must be enabled for atomic transactions.
    if (_store.Options.Wal?.Enabled != true)
      throw new InvalidOperationException(
        "Transactions require the Write-Ahead Log to be enabled. Set StoreOptions.Wal.Enabled = true.");

    // 1. Calculate WAL buffer size.
    int totalWalSize = WalRecord.BeginTransactionSize + WalRecord.CommitTransactionSize;
    foreach (var operation in _operations)
    {
      if (operation.Type == OperationType.Insert)
      {
        var e = operation.Entry;
        totalWalSize += WalRecord.InsertRecordSize(
          e.Id, e.CollectionName,
          e.Content.IsEmpty ? null : e.Content.ToArray(),
          e.Embedding.IsEmpty ? null : e.Embedding.ToArray());
      }
      else
      {
        totalWalSize += WalRecord.DeleteRecordSize(operation.Key, operation.Collection);
      }
    }

    // 2. Serialize: [BEGIN][inserts...][deletes...][COMMIT]
    byte[] walBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(totalWalSize);
    try
    {
      int pos = 0;
      pos += WalRecord.SerializeBeginTransaction(walBuffer.AsSpan(pos), _txId);

      foreach (var operation in _operations)
      {
        pos += operation.Type == OperationType.Insert
          ? SerializeInsert(walBuffer.AsSpan(pos), operation.Entry)
          : WalRecord.SerializeDelete(walBuffer.AsSpan(pos), operation.Key, operation.Collection);
      }

      pos += WalRecord.SerializeCommitTransaction(walBuffer.AsSpan(pos), _txId);

      lock (_store.WalLock)
      {
        // The complete transaction is one indivisible WAL append.
        _store.WalFileStream!.Write(walBuffer, 0, pos);
        StoreWritingExtensions.CompleteWalWrite(_store);
        // From this point the transaction is durably represented in the WAL
        // (according to the selected policy) and must not be submitted twice,
        // even if applying it to the live pipeline subsequently fails.
        _committed = true;

        // Register operations in their original order while the checkpointer
        // is excluded. Before a delete, drain preceding inserts so it cannot
        // be overtaken and later resurrect the key.
        foreach (var operation in _operations)
        {
          if (operation.Type == OperationType.Insert)
          {
            _store.IngestionQueue.Enqueue(operation.Entry);
            _store.Writer.SignalNewData();
          }
          else
          {
            StoreWritingExtensions.DrainPipeline(_store);
            StoreWritingExtensions.DeleteContentCore(
              _store, operation.Collection, operation.Key);
          }
        }
      }

    }
    finally
    {
      System.Buffers.ArrayPool<byte>.Shared.Return(walBuffer);
    }

    await Task.CompletedTask;
  }

  /// <summary>
  /// Discards all buffered operations without writing anything to the WAL.
  /// </summary>
  public void Rollback()
  {
    if (_disposed) return;
    _committed = true; // prevent double-commit
    _operations.Clear();
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
