using System.Runtime.CompilerServices;

namespace Jigen.Indexer.Extensions;

public static class Tools
{
  /// <summary>
  /// Distance is Lower Than.
  /// </summary>
  /// <param name="x">Left argument.</param>
  /// <param name="y">Right argument.</param>
  /// <returns>True if x &lt; y.</returns>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool DLt(float x, float y)
  {
    return x.CompareTo(y) < 0;
  }

  /// <summary>
  /// Distance is Greater Than.
  /// </summary>
  /// <param name="x">Left argument.</param>
  /// <param name="y">Right argument.</param>
  /// <returns>True if x &gt; y.</returns>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool DGt(float x, float y)
  {
    return x.CompareTo(y) > 0;
  }


  /// <summary>
  /// Distances are Equal.
  /// </summary>
  /// <param name="x">Left argument.</param>
  /// <param name="y">Right argument.</param>
  /// <returns>True if x == y.</returns>
  public static bool DEq(float x, float y)
  {
    return x.CompareTo(y) == 0;
  }

  /// <summary>
  /// Runs breadth first search.
  /// </summary>
  /// <param name="entryPoint">The entry point.</param>
  /// <param name="level">The level of the graph where to run BFS.</param>
  /// <param name="visitAction">The action to perform on each node.</param>
  internal static void BFS(IndexNode entryPoint, int level, Action<IndexNode> visitAction, SmallWorld smallworld)
  {
    var visitedIds = new HashSet<int>();
    var expansionQueue = new Queue<IndexNode>(new[] { entryPoint });

    while (expansionQueue.Any())
    {
      var currentNode = expansionQueue.Dequeue();
      if (!visitedIds.Contains(currentNode.PositionId))
      {
        visitAction(currentNode);
        visitedIds.Add(currentNode.PositionId);
        foreach (var neighbour in currentNode.GetConnections(level, smallworld))
        {
          expansionQueue.Enqueue(neighbour);
        }
      }
    }
  }
}