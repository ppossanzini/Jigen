using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public class StoreLifecycleTests
{
  [Fact]
  public async Task Close_DrainsPendingWrites_WithoutExplicitSave()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-lifecycle-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid().ToByteArray()).ToArray();

      var store = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path });

      for (var i = 0; i < ids.Length; i++)
        await store.AppendContent(new VectorEntry
        {
          Id = ids[i],
          CollectionName = "docs",
          Content = Encoding.UTF8.GetBytes($"content-{i}"),
          Embedding = new[] { i, i + 1f }
        });

      // No SaveChangesAsync: Close alone must drain the ingestion queue
      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path });

      for (var i = 0; i < ids.Length; i++)
      {
        Assert.True(reopened.TryGetContent("docs", ids[i], out var content));
        Assert.Equal($"content-{i}", Encoding.UTF8.GetString(content));
      }

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task Dispose_IsIdempotent_AndClosesTheStore()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-lifecycle-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var id = Guid.NewGuid().ToByteArray();

      using (var store = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path }))
      {
        await store.AppendContent(new VectorEntry
        {
          Id = id,
          CollectionName = "docs",
          Content = "via-dispose"u8.ToArray(),
          Embedding = new[] { 1f, 2f }
        });
        // Dispose (from using) must flush everything; a redundant Close must not throw
        await store.Close();
      }

      var reopened = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path });
      Assert.True(reopened.TryGetContent("docs", id, out var content));
      Assert.Equal("via-dispose"u8.ToArray(), content);
      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task CollectionClear_IsPersistedAcrossReopen()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-lifecycle-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var store = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path });

      var keepId = Guid.NewGuid().ToByteArray();
      await store.AppendContent(new VectorEntry
        { Id = keepId, CollectionName = "keepers", Content = "keep"u8.ToArray(), Embedding = new[] { 1f } });
      for (var i = 0; i < 10; i++)
        await store.AppendContent(new VectorEntry
          { Id = Guid.NewGuid().ToByteArray(), CollectionName = "wiped", Content = "gone"u8.ToArray(), Embedding = new[] { 2f } });
      await store.SaveChangesAsync();

      Assert.Equal(10, await store.ClearContent("wiped"));
      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "lifecycle", DataBasePath = path });

      Assert.DoesNotContain("wiped", reopened.GetCollections());
      Assert.True(reopened.TryGetContent("keepers", keepId, out _));

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
