using Jigen.PerformancePrimitives;
using Xunit;

namespace PrimitiveTests;

public class TestCircularMemoryQueue
{
  private sealed record Item(int Producer, int Value);

  [Fact]
  public async Task ConcurrentProducersAndConsumers_DeliverEveryItemExactlyOnce()
  {
    const int producers = 8;
    const int consumers = 4;
    const int itemsPerProducer = 50_000;
    const int totalItems = producers * itemsPerProducer;

    // Small capacity forces many laps around the ring, maximizing contention
    // on slot reuse between producers and consumers.
    var queue = new CircularMemoryQueue<Item>(512);

    var consumed = new int[producers][];
    for (int p = 0; p < producers; p++)
      consumed[p] = new int[itemsPerProducer];

    int consumedCount = 0;

    var consumerTasks = Enumerable.Range(0, consumers).Select(_ => Task.Run(() =>
    {
      while (Volatile.Read(ref consumedCount) < totalItems)
      {
        if (!queue.TryDequeue(out var item)) continue;

        Assert.NotNull(item);
        Interlocked.Increment(ref consumed[item.Producer][item.Value]);
        Interlocked.Increment(ref consumedCount);
      }
    })).ToArray();

    var producerTasks = Enumerable.Range(0, producers).Select(p => Task.Run(() =>
    {
      for (int i = 0; i < itemsPerProducer; i++)
        queue.Enqueue(new Item(p, i));
    })).ToArray();

    await Task.WhenAll(producerTasks);
    await Task.WhenAll(consumerTasks).WaitAsync(TimeSpan.FromSeconds(60));

    for (int p = 0; p < producers; p++)
      for (int i = 0; i < itemsPerProducer; i++)
        Assert.Equal(1, consumed[p][i]);

    Assert.True(queue.IsEmpty);
  }
}
