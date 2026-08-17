using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;

namespace JigenTests;

public class StoreReliabilityTests
{
  private static string NewTempPath() =>
    Path.Combine(Path.GetTempPath(), $"jigen-reliability-test-{Guid.NewGuid():N}");

  [Fact]
  public async Task BulkAppend_SaveChangesWaitsForEveryEntryAcrossBatches()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(new StoreOptions { DataBaseName = "bulk", DataBasePath = path });

      static VectorEntry Entry(int i) => new()
      {
        Id = BitConverter.GetBytes(i), CollectionName = "docs",
        Content = BitConverter.GetBytes(i), Embedding = new[] { (float)i }
      };

      await store.AppendContentBulk(Enumerable.Range(0, 512).Select(Entry).ToArray());
      await store.SaveChangesAsync();
      await store.AppendContentBulk(Enumerable.Range(512, 512).Select(Entry).ToArray());
      await store.SaveChangesAsync();

      Assert.True(store.GetCollectionIndexOf("docs", out var index));
      Assert.Equal(1024, index.Count);
      Assert.Equal(0, store.IngestionQueueLength);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task ConcurrentWalAppendsAndTransactionsRemainReplayable()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var options = new StoreOptions
      {
        DataBaseName = "wal-concurrent", DataBasePath = path,
        Wal = new WalOptions
        {
          Enabled = true, Durability = WalDurability.PerWrite,
          CheckpointInterval = TimeSpan.FromMilliseconds(5)
        }
      };
      var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid().ToByteArray()).ToArray();

      using (var store = new Store(options))
      {
        await Task.WhenAll(ids.Take(50).Select((id, i) =>
          store.AppendContent(new VectorEntry
          {
            Id = id, CollectionName = "docs",
            Content = BitConverter.GetBytes(i), Embedding = new[] { (float)i }
          })));

        await Task.WhenAll(ids.Skip(50).Select(async (id, i) =>
        {
          using var tx = store.BeginTransaction();
          tx.Append(new VectorEntry
          {
            Id = id, CollectionName = "docs",
            Content = BitConverter.GetBytes(i + 50), Embedding = new[] { (float)(i + 50) }
          });
          await tx.CommitAsync();
        }));

        await store.SaveChangesAsync();
      }

      using var reopened = new Store(options);
      Assert.True(reopened.GetCollectionIndexOf("docs", out var index));
      Assert.Equal(ids.Length, index.Count);
      Assert.All(ids, id => Assert.True(reopened.TryGetContent("docs", id, out _)));
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task WalCheckpointActuallyReclaimsCheckpointedRecords()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);
    try
    {
      var options = new StoreOptions
      {
        DataBaseName = "wal-truncate", DataBasePath = path,
        Wal = new WalOptions
        {
          Enabled = true, Durability = WalDurability.PerWrite,
          CheckpointInterval = TimeSpan.FromMilliseconds(20)
        }
      };
      using var store = new Store(options);
      await store.AppendContent(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(), CollectionName = "docs",
        Content = "payload"u8.ToArray(), Embedding = new[] { 1f, 2f }
      });
      await store.SaveChangesAsync();

      var walPath = Path.Combine(path, "wal-truncate.wal.jigen");
      Assert.True(SpinWait.SpinUntil(() => new FileInfo(walPath).Length == 0, TimeSpan.FromSeconds(2)));
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task CollectionRejectsMixedVectorDimensionsBeforePersistence()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(new StoreOptions { DataBaseName = "dimensions", DataBasePath = path });
      await store.AppendContent(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(), CollectionName = "docs",
        Content = "valid"u8.ToArray(), Embedding = new[] { 1f, 2f, 3f }
      });

      await Assert.ThrowsAsync<ArgumentException>(() => store.AppendContent(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(), CollectionName = "docs",
        Content = "invalid"u8.ToArray(), Embedding = new[] { 1f, 2f }
      }));
      await store.SaveChangesAsync();
      Assert.Equal(1, store.GetCollectionInfo("docs").Vectors);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SecondOpen_OfSameDatabase_Throws_AndSucceedsAfterClose()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);

    try
    {
      var store = new Store(new StoreOptions { DataBaseName = "excl", DataBasePath = path });

      // A second instance on the same files would corrupt them silently:
      // it must be rejected while the first is open.
      Assert.Throws<IOException>(() => new Store(new StoreOptions { DataBaseName = "excl", DataBasePath = path }));

      // A different database name in the same directory is fine.
      var other = new Store(new StoreOptions { DataBaseName = "other", DataBasePath = path });
      await other.Close();

      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "excl", DataBasePath = path });
      Assert.False(reopened.WasUncleanShutdown);
      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task AppendThenImmediateDelete_DoesNotResurrectTheEntry()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);

    try
    {
      var id = Guid.NewGuid().ToByteArray();
      var store = new Store(new StoreOptions { DataBaseName = "delorder", DataBasePath = path });

      // The append is only queued at this point: the delete must still win,
      // because the user issued it after the append.
      await store.AppendContent(new VectorEntry
      {
        Id = id, CollectionName = "docs", Content = "payload"u8.ToArray(), Embedding = new float[] { 1f, 2f, 3f }
      });
      Assert.True(await store.DeleteContent("docs", id));

      await store.SaveChangesAsync();
      Assert.False(store.TryGetContent("docs", id, out _));
      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "delorder", DataBasePath = path });
      Assert.False(reopened.TryGetContent("docs", id, out _));
      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  private static StoreOptions HnswOptionsFor(string path) => new()
  {
    DataBaseName = "recon",
    DataBasePath = path,
    Indexer = new SmallWorldIndexer(new SmallWorldOptions { StoragePath = Path.Combine(path, "hnsw") })
  };

  private static async Task<(byte[] a, byte[] b, byte[] c)> SeedThreeVectors(string path)
  {
    var a = Guid.NewGuid().ToByteArray();
    var b = Guid.NewGuid().ToByteArray();
    var c = Guid.NewGuid().ToByteArray();

    var store = new Store(HnswOptionsFor(path));
    await store.AppendContent(new VectorEntry
    {
      Id = a, CollectionName = "docs", Content = "alpha"u8.ToArray(), Embedding = new float[] { 1f, 0f, 0f }
    });
    await store.AppendContent(new VectorEntry
    {
      Id = b, CollectionName = "docs", Content = "beta"u8.ToArray(), Embedding = new float[] { 0f, 1f, 0f }
    });
    await store.AppendContent(new VectorEntry
    {
      Id = c, CollectionName = "docs", Content = "gamma"u8.ToArray(), Embedding = new float[] { 0f, 0f, 1f }
    });
    await store.SaveChangesAsync();
    await store.Close();

    return (a, b, c);
  }

  private static void DeleteGraphFiles(string path)
  {
    foreach (var file in Directory.GetFiles(Path.Combine(path, "hnsw")))
      File.Delete(file);
  }

  [Fact]
  public async Task UncleanShutdown_AutomaticallyReindexesLostGraph()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);

    try
    {
      var (a, _, _) = await SeedThreeVectors(path);

      // Simulate a crash that lost every graph update: the graph files are
      // gone and the surviving lock file marks the shutdown as unclean.
      DeleteGraphFiles(path);
      File.Create(Path.Combine(path, "recon.lock.jigen")).Dispose();

      var reopened = new Store(HnswOptionsFor(path));
      Assert.True(reopened.WasUncleanShutdown);

      var results = reopened.Search("docs", new[] { 1f, 0f, 0f }, 1).ToList();
      Assert.Single(results);
      Assert.Equal(a, results[0].entry.Id);

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task ManualReconcile_ReindexesLostGraph()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);

    try
    {
      var (a, _, _) = await SeedThreeVectors(path);
      DeleteGraphFiles(path);

      // Clean shutdown: no automatic reconcile, the lost graph shows up as
      // empty search results until ReconcileIndexAsync is invoked.
      var reopened = new Store(HnswOptionsFor(path));
      Assert.False(reopened.WasUncleanShutdown);
      Assert.Empty(reopened.Search("docs", new[] { 1f, 0f, 0f }, 1));

      await reopened.ReconcileIndexAsync();

      var results = reopened.Search("docs", new[] { 1f, 0f, 0f }, 1).ToList();
      Assert.Single(results);
      Assert.Equal(a, results[0].entry.Id);

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task Reconcile_DropsGraphNodes_DeletedFromTheStore()
  {
    var path = NewTempPath();
    Directory.CreateDirectory(path);

    try
    {
      var (a, b, _) = await SeedThreeVectors(path);

      // Snapshot the graph while it still contains all three vectors.
      var hnswPath = Path.Combine(path, "hnsw");
      var backupPath = Path.Combine(path, "hnsw-backup");
      Directory.CreateDirectory(backupPath);
      foreach (var file in Directory.GetFiles(hnswPath))
        File.Copy(file, Path.Combine(backupPath, Path.GetFileName(file)));

      // Delete B normally, then restore the stale graph and fake a crash:
      // the store no longer knows B but the graph still does — the exact
      // divergence left by a crash before the graph flushed the delete.
      var store = new Store(HnswOptionsFor(path));
      Assert.True(await store.DeleteContent("docs", b));
      await store.SaveChangesAsync();
      await store.Close();

      foreach (var file in Directory.GetFiles(backupPath))
        File.Copy(file, Path.Combine(hnswPath, Path.GetFileName(file)), overwrite: true);
      File.Create(Path.Combine(path, "recon.lock.jigen")).Dispose();

      var reopened = new Store(HnswOptionsFor(path));
      Assert.True(reopened.WasUncleanShutdown);

      var results = reopened.Search("docs", new[] { 0f, 1f, 0f }, 3).ToList();
      Assert.DoesNotContain(results, r => r.entry.Id.SequenceEqual(b));
      Assert.Contains(results, r => r.entry.Id.SequenceEqual(a));

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
