using Jigen.Persistance;

namespace PrimitiveTests;

public class TestStoredListLookup
{
  private static StoredList<TestItem, TestItemOptions> CreateList(out string filePath)
  {
    filePath = Path.Combine(Path.GetTempPath(), $"jigen-storedlist-lookup-{Guid.NewGuid():N}.list");
    return new StoredList<TestItem, TestItemOptions>(new StoreListOptions { FilePath = filePath }, new TestItemOptions());
  }

  private static void Cleanup(string filePath)
  {
    if (File.Exists(filePath)) File.Delete(filePath);
    if (File.Exists($"{filePath}.index")) File.Delete($"{filePath}.index");
  }

  [Fact]
  public async Task IndexOfAndContains_FindTheRightItems()
  {
    var list = CreateList(out var filePath);
    try
    {
      for (var i = 0; i < 100; i++)
        list.Add(new TestItem { Id = i, Name = $"item-{i}" });

      Assert.Equal(0, list.IndexOf(new TestItem { Id = 0, Name = "item-0" }));
      Assert.Equal(57, list.IndexOf(new TestItem { Id = 57, Name = "item-57" }));
      Assert.True(list.Contains(new TestItem { Id = 99, Name = "item-99" }));

      Assert.Equal(-1, list.IndexOf(new TestItem { Id = 57, Name = "different" }));
      Assert.False(list.Contains(new TestItem { Id = 100, Name = "item-100" }));
      Assert.Equal(-1, list.IndexOf(null));
    }
    finally
    {
      await list.DisposeAsync();
      Cleanup(filePath);
    }
  }

  [Fact]
  public async Task Remove_RemovesTheRightItem_AndLookupSurvivesTheShift()
  {
    var list = CreateList(out var filePath);
    try
    {
      for (var i = 0; i < 10; i++)
        list.Add(new TestItem { Id = i, Name = $"item-{i}" });

      Assert.True(list.Remove(new TestItem { Id = 4, Name = "item-4" }));
      Assert.Equal(9, list.Count);
      Assert.False(list.Contains(new TestItem { Id = 4, Name = "item-4" }));

      // Indices shifted: the hash index must rebuild and stay coherent
      Assert.Equal(4, list.IndexOf(new TestItem { Id = 5, Name = "item-5" }));
      Assert.Equal(8, list.IndexOf(new TestItem { Id = 9, Name = "item-9" }));

      Assert.False(list.Remove(new TestItem { Id = 4, Name = "item-4" }));
      Assert.False(list.Remove(null));
    }
    finally
    {
      await list.DisposeAsync();
      Cleanup(filePath);
    }
  }

  [Fact]
  public async Task Lookup_TracksUpdatesThroughTheSetter()
  {
    var list = CreateList(out var filePath);
    try
    {
      for (var i = 0; i < 10; i++)
        list.Add(new TestItem { Id = i, Name = $"item-{i}" });

      // Build the lazy hash index, then mutate through the setter
      Assert.Equal(3, list.IndexOf(new TestItem { Id = 3, Name = "item-3" }));

      list[3] = new TestItem { Id = 3, Name = "updated" };

      Assert.Equal(-1, list.IndexOf(new TestItem { Id = 3, Name = "item-3" }));
      Assert.Equal(3, list.IndexOf(new TestItem { Id = 3, Name = "updated" }));
    }
    finally
    {
      await list.DisposeAsync();
      Cleanup(filePath);
    }
  }

  [Fact]
  public async Task Lookup_WorksOnAReloadedList()
  {
    var list = CreateList(out var filePath);
    try
    {
      for (var i = 0; i < 10; i++)
        list.Add(new TestItem { Id = i, Name = $"item-{i}" });
      await list.DisposeAsync();

      var reloaded = new StoredList<TestItem, TestItemOptions>(new StoreListOptions { FilePath = filePath }, new TestItemOptions());
      try
      {
        Assert.Equal(7, reloaded.IndexOf(new TestItem { Id = 7, Name = "item-7" }));
        Assert.True(reloaded.Contains(new TestItem { Id = 0, Name = "item-0" }));
        Assert.False(reloaded.Contains(new TestItem { Id = 0, Name = "nope" }));
      }
      finally
      {
        await reloaded.DisposeAsync();
      }
    }
    finally
    {
      Cleanup(filePath);
    }
  }
}
