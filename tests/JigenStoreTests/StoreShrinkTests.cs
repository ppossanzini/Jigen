using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public class StoreShrinkTests
{
  private static StoreOptions OptionsFor(string path) => new()
  {
    DataBaseName = "shrinktest",
    DataBasePath = path,
    ShrinkMinDeadBytes = 1,
    ShrinkFragmentationThreshold = 0.01
  };

  private static VectorEntry Entry(byte[] id, int seed) => new()
  {
    Id = id,
    CollectionName = "docs",
    Content = Encoding.UTF8.GetBytes($"content-{seed}-" + new string('x', 512)),
    Embedding = new[] { seed, seed + 1f, seed + 2f, seed + 3f }
  };

  [Fact]
  public async Task Shrink_ReclaimsSpace_AndPreservesLiveData()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-shrink-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid().ToByteArray()).ToArray();

      var store = new Store(OptionsFor(path));

      for (var i = 0; i < ids.Length; i++)
        await store.AppendContent(Entry(ids[i], i));
      await store.SaveChangesAsync();

      // Delete the first 25, overwrite 10 of the survivors
      for (var i = 0; i < 25; i++)
        Assert.True(await store.DeleteContent("docs", ids[i]));

      for (var i = 25; i < 35; i++)
        await store.AppendContent(Entry(ids[i], i + 1000));
      await store.SaveChangesAsync();

      var contentSizeBefore = new FileInfo(store.GetFileNames().ElementAt(1)).Length;
      var embeddingsSizeBefore = new FileInfo(store.GetFileNames().ElementAt(2)).Length;

      Assert.True(store.DeadBytes > 0);
      Assert.True(store.NeedsShrink);

      Assert.True(await store.ShrinkAsync());

      Assert.Equal(0, store.DeadBytes);
      Assert.False(store.NeedsShrink);
      Assert.True(new FileInfo(store.GetFileNames().ElementAt(1)).Length < contentSizeBefore);
      Assert.True(new FileInfo(store.GetFileNames().ElementAt(2)).Length < embeddingsSizeBefore);

      // Deleted entries are gone, survivors readable with the right content
      for (var i = 0; i < 25; i++)
        Assert.False(store.TryGetContent("docs", ids[i], out _));

      for (var i = 25; i < ids.Length; i++)
      {
        Assert.True(store.TryGetContent("docs", ids[i], out var content));
        var expectedSeed = i < 35 ? i + 1000 : i;
        Assert.StartsWith($"content-{expectedSeed}-", Encoding.UTF8.GetString(content));
      }

      // Ingestion keeps working on the swapped files
      var extraId = Guid.NewGuid().ToByteArray();
      await store.AppendContent(Entry(extraId, 9999));
      await store.SaveChangesAsync();
      Assert.True(store.TryGetContent("docs", extraId, out _));

      await store.Close();

      // Everything survives a reopen (index log rewritten by the shrink)
      var reopened = new Store(OptionsFor(path));

      Assert.Equal(0, reopened.DeadBytes);
      for (var i = 0; i < 25; i++)
        Assert.False(reopened.TryGetContent("docs", ids[i], out _));
      for (var i = 25; i < ids.Length; i++)
        Assert.True(reopened.TryGetContent("docs", ids[i], out _));
      Assert.True(reopened.TryGetContent("docs", extraId, out _));

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task AutoShrink_RunsDuringSaveChanges_WhenThresholdsExceeded()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-shrink-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var options = OptionsFor(path);
      options.AutoShrink = true;

      var store = new Store(options);

      var keepId = Guid.NewGuid().ToByteArray();
      var deleteId = Guid.NewGuid().ToByteArray();

      await store.AppendContent(Entry(keepId, 1));
      await store.AppendContent(Entry(deleteId, 2));
      await store.SaveChangesAsync();

      Assert.True(await store.DeleteContent("docs", deleteId));
      Assert.True(store.NeedsShrink);

      await store.SaveChangesAsync();

      Assert.Equal(0, store.DeadBytes);
      Assert.True(store.TryGetContent("docs", keepId, out _));
      Assert.False(store.TryGetContent("docs", deleteId, out _));

      await store.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
