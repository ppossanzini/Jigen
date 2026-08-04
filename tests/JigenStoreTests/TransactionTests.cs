using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public class TransactionTests
{
  private static string CreateTempPath() =>
    Path.Combine(Path.GetTempPath(), $"jigen-tx-test-{Guid.NewGuid():N}");

  private static StoreOptions WalEnabledOptions(string path) => new()
  {
    DataBaseName = "txtest",
    DataBasePath = path,
    Wal = new WalOptions
    {
      Enabled = true,
      Durability = WalDurability.PerWrite,
      CheckpointInterval = TimeSpan.FromMinutes(1) // disable auto-checkpoint during tests
    }
  };

  // ── Commit: all-or-nothing ──

  [Fact]
  public async Task Commit_WritesAllEntriesAtomically()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid().ToByteArray()).ToArray();

      using (var store = new Store(WalEnabledOptions(path)))
      {
        using var tx = store.BeginTransaction();
        for (int i = 0; i < ids.Length; i++)
          tx.Append(new VectorEntry
          {
            Id = ids[i],
            CollectionName = "docs",
            Content = Encoding.UTF8.GetBytes($"doc-{i}"),
            Embedding = new[] { i, i + 1f, i + 0.5f }
          });
        await tx.CommitAsync();
        await store.SaveChangesAsync();
      }

      // Reopen: all entries must be visible
      using (var store = new Store(WalEnabledOptions(path)))
      {
        for (int i = 0; i < ids.Length; i++)
        {
          Assert.True(store.TryGetContent("docs", ids[i], out var content),
            $"Entry {i} not found after commit");
          Assert.Equal($"doc-{i}", Encoding.UTF8.GetString(content));
        }
      }
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Rollback: nothing persisted ──

  [Fact]
  public async Task Rollback_DiscardsBufferedOperations()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var persistedId = Guid.NewGuid().ToByteArray();
      var rolledBackId = Guid.NewGuid().ToByteArray();

      using (var store = new Store(WalEnabledOptions(path)))
      {
        // Write one entry normally (outside transaction)
        await store.AppendContent(new VectorEntry
        {
          Id = persistedId,
          CollectionName = "docs",
          Content = "persisted"u8.ToArray(),
          Embedding = new[] { 1f }
        });

        // Write another inside a rolled-back transaction
        using var tx = store.BeginTransaction();
        tx.Append(new VectorEntry
        {
          Id = rolledBackId,
          CollectionName = "docs",
          Content = "rolledback"u8.ToArray(),
          Embedding = new[] { 2f }
        });
        tx.Rollback();

        await store.SaveChangesAsync();
      }

      // Reopen: only persistedId must exist
      using (var store = new Store(WalEnabledOptions(path)))
      {
        Assert.True(store.TryGetContent("docs", persistedId, out _),
          "Persisted entry should exist");
        Assert.False(store.TryGetContent("docs", rolledBackId, out _),
          "Rolled-back entry must not exist");
      }
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Dispose rolls back ──

  [Fact]
  public async Task Dispose_RollsBack_WhenNotCommitted()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var rolledBackId = Guid.NewGuid().ToByteArray();

      using (var store = new Store(WalEnabledOptions(path)))
      {
        // using without CommitAsync -> Dispose calls Rollback
        using (var tx = store.BeginTransaction())
        {
          tx.Append(new VectorEntry
          {
            Id = rolledBackId,
            CollectionName = "docs",
            Content = "forgotten"u8.ToArray(),
            Embedding = new[] { 1f }
          });
        }

        await store.SaveChangesAsync();
      }

      using (var store = new Store(WalEnabledOptions(path)))
      {
        Assert.False(store.TryGetContent("docs", rolledBackId, out _),
          "Disposed transaction must not persist");
      }
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Recovery: incomplete transaction is rolled back ──

  [Fact]
  public async Task Recovery_RollsBackIncompleteTransaction()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var rolledBackId = Guid.NewGuid().ToByteArray();

      // 1. Write a complete entry first (as baseline)
      using (var store = new Store(WalEnabledOptions(path)))
      {
        await store.AppendContent(new VectorEntry
        {
          Id = rolledBackId,
          CollectionName = "docs",
          Content = "before-tx"u8.ToArray(),
          Embedding = new[] { 1f }
        });
        await store.SaveChangesAsync();
      }

      // 2. Manually inject a BEGIN + INSERT (no COMMIT) into the WAL file
      //    to simulate a crash mid-transaction.
      var walPath = Path.Combine(path, "txtest.wal.jigen");
      using (var walStream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
      {
        walStream.Seek(0, SeekOrigin.End);

        var txId = Guid.NewGuid();
        var fakeId = Guid.NewGuid().ToByteArray();

        byte[] buffer = new byte[
          WalRecord.BeginTransactionSize +
          WalRecord.InsertRecordSize(fakeId, "docs",
            Encoding.UTF8.GetBytes("mid-tx"),
            new[] { 9f, 9f, 9f })
        ];

        int pos = 0;
        pos += WalRecord.SerializeBeginTransaction(buffer.AsSpan(pos), txId);
        pos += WalRecord.SerializeInsert(buffer.AsSpan(pos), fakeId, "docs",
          Encoding.UTF8.GetBytes("mid-tx"),
          new[] { 9f, 9f, 9f });
        // Intentionally no COMMIT — simulates crash

        walStream.Write(buffer, 0, pos);
        walStream.Flush(true);
      }

      // 3. Reopen: the incomplete tx must be rolled back, the WAL truncated.
      using (var store = new Store(WalEnabledOptions(path)))
      {
        // The pre-tx entry must still be there
        Assert.True(store.TryGetContent("docs", rolledBackId, out var content),
          "Pre-transaction entry must survive recovery");
        Assert.Equal("before-tx", Encoding.UTF8.GetString(content));
      }
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Mixed inserts and deletes ──

  [Fact]
  public async Task Commit_AppliesInsertsAndDeletesAtomically()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var keepId = Guid.NewGuid().ToByteArray();
      var deleteId = Guid.NewGuid().ToByteArray();
      var insertId = Guid.NewGuid().ToByteArray();

      // First, create two entries normally
      using (var store = new Store(WalEnabledOptions(path)))
      {
        await store.AppendContent(new VectorEntry
        {
          Id = keepId, CollectionName = "docs",
          Content = "keep"u8.ToArray(), Embedding = new[] { 1f }
        });
        await store.AppendContent(new VectorEntry
        {
          Id = deleteId, CollectionName = "docs",
          Content = "delete-me"u8.ToArray(), Embedding = new[] { 2f }
        });
        await store.SaveChangesAsync();
      }

      // Now: transaction that deletes one and inserts another
      using (var store = new Store(WalEnabledOptions(path)))
      {
        using var tx = store.BeginTransaction();
        tx.Delete("docs", deleteId);
        tx.Append(new VectorEntry
        {
          Id = insertId, CollectionName = "docs",
          Content = "inserted"u8.ToArray(), Embedding = new[] { 3f }
        });
        await tx.CommitAsync();
        await store.SaveChangesAsync();
      }

      // Verify
      using (var store = new Store(WalEnabledOptions(path)))
      {
        Assert.True(store.TryGetContent("docs", keepId, out _), "keepId must survive");
        Assert.False(store.TryGetContent("docs", deleteId, out _), "deleteId must be gone");
        Assert.True(store.TryGetContent("docs", insertId, out var content), "insertId must exist");
        Assert.Equal("inserted", Encoding.UTF8.GetString(content));
      }
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Empty transaction ──

  [Fact]
  public async Task Commit_EmptyTransaction_IsNoop()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(WalEnabledOptions(path));
      using var tx = store.BeginTransaction();
      // no Append / Delete
      await tx.CommitAsync(); // must not throw
      await store.SaveChangesAsync();
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Double commit throws ──

  [Fact]
  public async Task Commit_CalledTwice_Throws()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(WalEnabledOptions(path));
      using var tx = store.BeginTransaction();
      tx.Append(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(),
        CollectionName = "docs",
        Content = "test"u8.ToArray(),
        Embedding = new[] { 1f }
      });
      await tx.CommitAsync();
      await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Rollback then commit throws ──

  [Fact]
  public async Task Commit_AfterRollback_Throws()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(WalEnabledOptions(path));
      using var tx = store.BeginTransaction();
      tx.Append(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(),
        CollectionName = "docs",
        Content = "test"u8.ToArray(),
        Embedding = new[] { 1f }
      });
      tx.Rollback();
      await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Append after commit throws ──

  [Fact]
  public async Task Append_AfterCommit_Throws()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(WalEnabledOptions(path));
      using var tx = store.BeginTransaction();
      await tx.CommitAsync();
      Assert.Throws<InvalidOperationException>(() => tx.Append(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(),
        CollectionName = "docs",
        Content = "late"u8.ToArray(),
        Embedding = new[] { 1f }
      }));
    }
    finally { Directory.Delete(path, recursive: true); }
  }

  // ── Transaction requires WAL ──

  [Fact]
  public async Task Commit_WithoutWal_Throws()
  {
    var path = CreateTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(new StoreOptions
      {
        DataBaseName = "nowal",
        DataBasePath = path
        // Wal not set -> Enabled defaults to false
      });
      using var tx = store.BeginTransaction();
      tx.Append(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(),
        CollectionName = "docs",
        Content = "test"u8.ToArray(),
        Embedding = new[] { 1f }
      });
      await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
    }
    finally { Directory.Delete(path, recursive: true); }
  }
}
