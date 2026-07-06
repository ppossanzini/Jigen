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
