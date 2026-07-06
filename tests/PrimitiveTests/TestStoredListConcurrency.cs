using Jigen.Persistance;

namespace PrimitiveTests;

public class TestStoredListConcurrency
{
  [Fact]
  public async Task ConcurrentReadsUpdatesAndShrink_NeverReadCorruptedItems()
  {
    var filePath = Path.Combine(Path.GetTempPath(), $"jigen-storedlist-race-{Guid.NewGuid():N}.list");

    try
    {
      await using var list = new StoredList<TestItem, TestItemOptions>(new StoreListOptions
      {
        FilePath = filePath,
      }, new TestItemOptions());

      const int itemCount = 200;
      for (var i = 0; i < itemCount; i++)
        list.Add(new TestItem { Id = i, Name = $"item-{i}" });

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
      var failures = new List<Exception>();

      // Readers: every deserialized item must be self-consistent (Name matches Id).
      // LongRunning → dedicated threads, independent from thread-pool pressure.
      var readers = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(() =>
      {
        var rnd = new Random();
        while (!cts.IsCancellationRequested)
        {
          var index = rnd.Next(itemCount);
          var item = list[index];
          if (item.Id != index || !item.Name.StartsWith($"item-{index}"))
            throw new InvalidOperationException($"Corrupted read at {index}: Id={item.Id}, Name={item.Name}");
        }
      }, TaskCreationOptions.LongRunning)).ToArray();

      // Updater: grows items so relocations + tombstones accumulate
      var updater = Task.Factory.StartNew(() =>
      {
        var rnd = new Random();
        var generation = 0;
        while (!cts.IsCancellationRequested)
        {
          generation++;
          var index = rnd.Next(itemCount);
          list[index] = new TestItem { Id = index, Name = $"item-{index}-gen{generation}" };
        }
      }, TaskCreationOptions.LongRunning);

      // Shrinker: compacts continuously while reads and writes are in flight
      var shrinker = Task.Factory.StartNew(() =>
      {
        while (!cts.IsCancellationRequested)
        {
          list.ShrinkDb();
          Thread.Sleep(10);
        }
      }, TaskCreationOptions.LongRunning);

      try
      {
        await Task.WhenAll(readers.Append(updater).Append(shrinker));
      }
      catch (Exception ex)
      {
        failures.Add(ex);
      }

      Assert.Empty(failures);

      // Final consistency sweep
      for (var i = 0; i < itemCount; i++)
      {
        var item = list[i];
        Assert.Equal(i, item.Id);
        Assert.StartsWith($"item-{i}", item.Name);
      }
    }
    finally
    {
      if (File.Exists(filePath)) File.Delete(filePath);
      if (File.Exists($"{filePath}.index")) File.Delete($"{filePath}.index");
    }
  }
}
