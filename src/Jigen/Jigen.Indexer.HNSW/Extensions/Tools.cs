using System.Runtime.CompilerServices;

namespace Jigen.Indexer.Extensions;

public static class Tools
{
  /// <summary>Distance is Lower Than.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool DLt(float x, float y) => x < y;

  /// <summary>Distance is Greater Than.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool DGt(float x, float y) => x > y;

  /// <summary>Distances are Equal.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool DEq(float x, float y) => x == y;

  /// <summary>
  /// Runs breadth first search starting from <paramref name="entryPoint"/> at the given layer level.
  /// </summary>
  internal static void BFS(IndexNode entryPoint, int level, Action<IndexNode> visitAction, SmallWorldIndexer smallworld, string collection)
  {
    var visitedIds = new HashSet<int>();
    var expansionQueue = new Queue<IndexNode>(new[] { entryPoint });
    var graph = smallworld.GetGraphForCollection(collection);

    while (expansionQueue.Count > 0)
    {
      var currentNode = expansionQueue.Dequeue();
      if (visitedIds.Add(currentNode.PositionId))
      {
        visitAction(currentNode);
        currentNode.ForEachConnection(level, graph, neighbour => expansionQueue.Enqueue(neighbour));
      }
    }
  }
}
