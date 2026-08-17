using Jigen.Indexer;

namespace HnswTest;

public class BinaryHeapTest
{
  [Fact]
  public void BottomUpHeapify_PopsItemsInDescendingOrder()
  {
    var random = new Random(42);
    var source = Enumerable.Range(0, 1_000)
      .Select(_ => random.Next())
      .ToArray();
    var expected = source.OrderByDescending(x => x).ToArray();

    var heap = new BinaryHeap<int>();
    heap.Initialize(source, static (left, right) => left.CompareTo(right));

    var actual = new int[source.Length];
    for (var i = 0; i < actual.Length; i++)
      actual[i] = heap.Pop();

    Assert.Equal(expected, actual);
  }
}
