using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexers;

namespace JigenTests;

public class SparseStorageTests
{
  private static string NewPath() =>
    Path.Combine(Path.GetTempPath(), $"jigen-sparse-{Guid.NewGuid():N}");

  private static StoreOptions OptionsFor(string path, string name = "sparse") => new()
  {
    DataBaseName = name,
    DataBasePath = path,
    Wal = new WalOptions { Enabled = true },
    SparseIndex = new SparseIndex()
  };

  private static SparseVector Vector(params (int term, float weight)[] pairs) =>
    SparseVector.FromPairs(pairs.Select(p => new KeyValuePair<int, float>(p.term, p.weight)));

  private static VectorEntry Entry(string key, SparseVector sparse, string collection = "docs") => new()
  {
    Id = Encoding.UTF8.GetBytes(key),
    CollectionName = collection,
    Content = Encoding.UTF8.GetBytes($"{{\"name\":\"{key}\"}}"),
    Embedding = new float[] { 1f, 0f, 0f },
    Sparse = sparse
  };

  [Fact]
  public async Task AppendContent_WithSparseAndNoWal_IsRejected()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(new StoreOptions { DataBaseName = "sparse", DataBasePath = path });

      var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        store.AppendContent(Entry("a", Vector((1, 1f)))));

      Assert.Contains("write-ahead log", error.Message, StringComparison.OrdinalIgnoreCase);
      await store.SaveChangesAsync();
      Assert.False(store.GetCollectionIndexOf("docs", out var index) && index.Count > 0);
      Assert.False(File.Exists(Path.Combine(path, "sparse.sparse.jigen")));
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SparseVector_RoundTripsWithinTheSameStore()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", Vector((3, 0.5f), (9, 2f))));
      await store.SaveChangesAsync();

      var read = store.GetSparse("docs", Encoding.UTF8.GetBytes("a"));

      Assert.NotNull(read);
      Assert.Equal([3, 9], read.Terms.ToArray());
      Assert.Equal([0.5f, 2f], read.Weights.ToArray());
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SparseVector_SurvivesReopen()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using (var store = new Store(OptionsFor(path)))
      {
        await store.AppendContent(Entry("a", Vector((1, 1f), (4, 3f))));
        await store.SaveChangesAsync();
        await store.Close();
      }

      using (var reopened = new Store(OptionsFor(path)))
      {
        var read = reopened.GetSparse("docs", Encoding.UTF8.GetBytes("a"));
        Assert.NotNull(read);
        Assert.Equal([1, 4], read.Terms.ToArray());
        Assert.Equal([1f, 3f], read.Weights.ToArray());
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SparseOnlyEntry_HasNoDenseVectorButKeepsItsTerms()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(new VectorEntry
      {
        Id = Encoding.UTF8.GetBytes("text-only"),
        CollectionName = "docs",
        Content = Encoding.UTF8.GetBytes("{\"name\":\"text-only\"}"),
        Sparse = Vector((5, 1.5f))
      });
      await store.SaveChangesAsync();

      Assert.Null(store.GetEmbedding("docs", Encoding.UTF8.GetBytes("text-only")));
      var read = store.GetSparse("docs", Encoding.UTF8.GetBytes("text-only"));
      Assert.NotNull(read);
      Assert.Equal(5, read.Terms.Span[0]);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task UpdatingAnEntry_ReplacesItsTerms()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", Vector((1, 1f), (2, 1f))));
      await store.SaveChangesAsync();

      await store.AppendContent(Entry("a", Vector((7, 4f))));
      await store.SaveChangesAsync();

      var read = store.GetSparse("docs", Encoding.UTF8.GetBytes("a"));
      Assert.NotNull(read);
      Assert.Equal([7], read.Terms.ToArray());
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task DeletingAnEntry_RemovesItsSparseVector_AfterReopen()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using (var store = new Store(OptionsFor(path)))
      {
        await store.AppendContent(Entry("a", Vector((1, 1f))));
        await store.AppendContent(Entry("b", Vector((1, 2f))));
        await store.SaveChangesAsync();

        Assert.True(await store.DeleteContent("docs", Encoding.UTF8.GetBytes("a")));
        await store.SaveChangesAsync();
        Assert.Null(store.GetSparse("docs", Encoding.UTF8.GetBytes("a")));
        await store.Close();
      }

      using (var reopened = new Store(OptionsFor(path)))
      {
        Assert.Null(reopened.GetSparse("docs", Encoding.UTF8.GetBytes("a")));
        Assert.NotNull(reopened.GetSparse("docs", Encoding.UTF8.GetBytes("b")));
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task Shrink_KeepsSparseVectorsAndReclaimsDeadBytes()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      var options = OptionsFor(path);
      options.ShrinkMinDeadBytes = 1;
      options.ShrinkFragmentationThreshold = 0.01;

      using var store = new Store(options);
      await store.AppendContent(Entry("a", Vector((1, 1f))));
      await store.AppendContent(Entry("a", Vector((2, 2f), (3, 3f))));
      await store.AppendContent(Entry("b", Vector((2, 1f))));
      await store.SaveChangesAsync();

      var deadBefore = store.DeadBytes;
      Assert.True(deadBefore > 0);

      Assert.True(await store.ShrinkAsync());

      var read = store.GetSparse("docs", Encoding.UTF8.GetBytes("a"));
      Assert.NotNull(read);
      Assert.Equal([2, 3], read.Terms.ToArray());
      Assert.Equal(0, store.DeadBytes);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task Checkpoint_KeepsSparseWritesDurable()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      var options = OptionsFor(path);
      options.Wal.CheckpointInterval = TimeSpan.FromMilliseconds(50);

      using (var store = new Store(options))
      {
        await store.AppendContent(Entry("a", Vector((1, 1f))));
        await store.SaveChangesAsync();

        // A checkpoint truncates the log: what survives must come from the files.
        await Task.Delay(200);
        await store.SaveChangesAsync();
        await store.Close();
      }

      using (var reopened = new Store(options))
      {
        var read = reopened.GetSparse("docs", Encoding.UTF8.GetBytes("a"));
        Assert.NotNull(read);
        Assert.Equal([1], read.Terms.ToArray());
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
