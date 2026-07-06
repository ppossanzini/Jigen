using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public class TornIndexLogTests
{
  private static StoreOptions OptionsFor(string path) =>
    new() { DataBaseName = "tornlog", DataBasePath = path };

  private static async Task<(byte[] first, byte[] second, string indexFile)> CreateStoreWithTwoEntries(string path)
  {
    var first = Guid.NewGuid().ToByteArray();
    var second = Guid.NewGuid().ToByteArray();

    var store = new Store(OptionsFor(path));
    var indexFile = store.GetFileNames().First();

    await store.AppendContent(new VectorEntry
    {
      Id = first, CollectionName = "docs", Content = "first"u8.ToArray(), Embedding = new float[] { 1f, 2f, 3f }
    });
    await store.AppendContent(new VectorEntry
    {
      Id = second, CollectionName = "docs", Content = "second"u8.ToArray(), Embedding = new float[] { 4f, 5f, 6f }
    });
    await store.SaveChangesAsync();
    await store.Close();

    return (first, second, indexFile);
  }

  [Fact]
  public async Task TornTail_IsTruncated_AndEntriesBeforeItSurvive()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-tornlog-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var (first, second, indexFile) = await CreateStoreWithTwoEntries(path);
      var cleanLength = new FileInfo(indexFile).Length;

      // Simulate a crash mid-append: a partial record (a plausible id length
      // followed by too few bytes) at the end of the log.
      using (var tail = new FileStream(indexFile, FileMode.Append, FileAccess.Write))
        tail.Write(new byte[] { 16, 0, 0, 0, 0xAB, 0xCD }, 0, 6);

      var reopened = new Store(OptionsFor(path));

      Assert.True(reopened.TryGetContent("docs", first, out var firstContent));
      Assert.Equal("first"u8.ToArray(), firstContent);
      Assert.True(reopened.TryGetContent("docs", second, out var secondContent));
      Assert.Equal("second"u8.ToArray(), secondContent);

      // The garbage tail must be gone so the next append starts on a clean
      // record boundary.
      Assert.Equal(cleanLength, new FileInfo(indexFile).Length);

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task GarbageTail_DoesNotPreventOpening_AndAppendsStillWork()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-tornlog-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var (first, _, indexFile) = await CreateStoreWithTwoEntries(path);

      // Garbage with a huge fake id length: without validation this would
      // drive a giant allocation or crash the constructor.
      using (var tail = new FileStream(indexFile, FileMode.Append, FileAccess.Write))
        tail.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 0, 16);

      var reopened = new Store(OptionsFor(path));
      Assert.True(reopened.TryGetContent("docs", first, out _));

      // The truncated log must accept new appends and survive another reopen.
      var third = Guid.NewGuid().ToByteArray();
      await reopened.AppendContent(new VectorEntry
      {
        Id = third, CollectionName = "docs", Content = "third"u8.ToArray(), Embedding = new float[] { 7f, 8f, 9f }
      });
      await reopened.SaveChangesAsync();
      await reopened.Close();

      var reopenedAgain = new Store(OptionsFor(path));
      Assert.True(reopenedAgain.TryGetContent("docs", first, out _));
      Assert.True(reopenedAgain.TryGetContent("docs", third, out var thirdContent));
      Assert.Equal("third"u8.ToArray(), thirdContent);
      await reopenedAgain.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task IndexerFailure_DoesNotKillTheWriter_AndSurfacesOnSaveChanges()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-tornlog-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);

    try
    {
      var store = new Store(new StoreOptions
      {
        DataBaseName = "tornlog",
        DataBasePath = path,
        Indexer = new ThrowingIndexer()
      });

      var id = Guid.NewGuid().ToByteArray();
      await store.AppendContent(new VectorEntry
      {
        Id = id, CollectionName = "docs", Content = "payload"u8.ToArray(), Embedding = new float[] { 1f, 2f, 3f }
      });

      // The indexer failure must not kill the writer thread: the entry is
      // persisted and the error surfaces here, once.
      var ex = await Assert.ThrowsAsync<IOException>(() => store.SaveChangesAsync());
      Assert.IsType<InvalidOperationException>(ex.InnerException);
      Assert.Null(store.IngestionError);
      Assert.True(store.TryGetContent("docs", id, out _));

      await store.Close();
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  private sealed class ThrowingIndexer : Jigen.IIndexer
  {
    public void AddToIndex(VectorEntry entry, bool waitForIndexing = false) =>
      throw new InvalidOperationException("boom");

    public void RemoveFromIndex(string collection, byte[] key) { }

    public IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top,
      Jigen.Filtering.IFilterExpression contentFilter = null) => [];

    public IEnumerable<VectorEntry> Search(IStore store, string collection, Jigen.Filtering.IFilterExpression contentFilter = null) => [];

    public Task FlushAsync() => Task.CompletedTask;

    public Task ShrinkAsync() => Task.CompletedTask;
  }
}
