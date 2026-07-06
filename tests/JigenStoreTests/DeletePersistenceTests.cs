using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public class DeletePersistenceTests
{
  [Fact]
  public async Task DeletedEntry_DoesNotResurrect_AfterReopen()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-delete-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var keepId = Guid.NewGuid().ToByteArray();
      var deleteId = Guid.NewGuid().ToByteArray();

      var store = new Store(new StoreOptions { DataBaseName = "deletetest", DataBasePath = path });

      await store.AppendContent(new VectorEntry
      {
        Id = keepId, CollectionName = "docs", Content = "keep-me"u8.ToArray(), Embedding = new float[] { 1f, 2f, 3f }
      });
      await store.AppendContent(new VectorEntry
      {
        Id = deleteId, CollectionName = "docs", Content = "delete-me"u8.ToArray(), Embedding = new float[] { 4f, 5f, 6f }
      });
      await store.SaveChangesAsync();

      Assert.True(await store.DeleteContent("docs", deleteId));
      Assert.False(store.TryGetContent("docs", deleteId, out _));

      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "deletetest", DataBasePath = path });

      Assert.False(reopened.TryGetContent("docs", deleteId, out _));
      Assert.True(reopened.TryGetContent("docs", keepId, out var keptContent));
      Assert.Equal("keep-me"u8.ToArray(), keptContent);

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task ReAddAfterDelete_SurvivesReopen()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-delete-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var id = Guid.NewGuid().ToByteArray();

      var store = new Store(new StoreOptions { DataBaseName = "deletetest", DataBasePath = path });

      await store.AppendContent(new VectorEntry
      {
        Id = id, CollectionName = "docs", Content = "first"u8.ToArray(), Embedding = new float[] { 1f, 2f, 3f }
      });
      await store.SaveChangesAsync();

      Assert.True(await store.DeleteContent("docs", id));

      await store.AppendContent(new VectorEntry
      {
        Id = id, CollectionName = "docs", Content = "second"u8.ToArray(), Embedding = new float[] { 4f, 5f, 6f }
      });
      await store.SaveChangesAsync();
      await store.Close();

      var reopened = new Store(new StoreOptions { DataBaseName = "deletetest", DataBasePath = path });

      Assert.True(reopened.TryGetContent("docs", id, out var content));
      Assert.Equal("second"u8.ToArray(), content);

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
