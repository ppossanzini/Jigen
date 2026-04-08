using Jigen.DataStructures;

namespace Jigen.Indexer.Extensions;

public static  class SmallWorldExtensions
{
  /// <summary>
  /// The implementaiton of SEARCH-LAYER(q, ep, ef, lc) algorithm.
  /// Article: Section 4. Algorithm 2.
  /// </summary>
  /// <param name="entryPoint">The entry point for the search.</param>
  /// <param name="destination">The search target.</param>
  /// <param name="k">The number of the nearest neighbours to get from the layer.</param>
  /// <param name="level">Level of the layer.</param>
  /// <returns>The list of the nearest neighbours at the level.</returns>
  public static IList<IndexNode> KNearestAtLevel(this SmallWorld smallworld, IndexNode entryPoint, IndexNode destination, int k, int level)
  {
    /*
     * v ← ep // set of visited elements
     * C ← ep // set of candidates
     * W ← ep // dynamic list of found nearest neighbors
     * while │C│ > 0
     *   c ← extract nearest element from C to q
     *   f ← get furthest element from W to q
     *   if distance(c, q) > distance(f, q)
     *     break // all elements in W are evaluated
     *   for each e ∈ neighbourhood(c) at layer lc // update C and W
     *     if e ∉ v
     *       v ← v ⋃ e
     *       f ← get furthest element from W to q
     *       if distance(e, q) < distance(f, q) or │W│ < ef
     *         C ← C ⋃ e
     *         W ← W ⋃ e
     *         if │W│ > ef
     *           remove furthest element from W to q
     * return W
     */

    // prepare tools
    TravelingCosts closerIsLess = destination.TravelingCosts;
    IComparer<IndexNode> fartherIsLess = closerIsLess.Reverse();

    // prepare heaps
    var resultHeap = new BinaryHeap<IndexNode>(new List<IndexNode>(k + 1) { entryPoint }, closerIsLess);
    var expansionHeap = new BinaryHeap<IndexNode>(new List<IndexNode>() { entryPoint }, fartherIsLess);

    // run bfs
    var visited = new HashSet<VectorKey>() { entryPoint.Id };
    while (expansionHeap.Buffer.Any())
    {
      // get next candidate to check and expand
      var toExpand = expansionHeap.Pop();
      var farthestResult = resultHeap.Buffer.First();
      if (Tools.DGt(destination.TravelingCosts.From(toExpand), destination.TravelingCosts.From(farthestResult)))
      {
        // the closest candidate is farther than farthest result
        break;
      }

      // expand candidate
      foreach (var neighbour in toExpand.GetConnections(level, smallworld))
      {
        if (!visited.Contains(neighbour.Id))
        {
          // enque perspective neighbours to expansion list
          farthestResult = resultHeap.Buffer.First();
          if (resultHeap.Buffer.Count < k
              || Tools.DLt(destination.TravelingCosts.From(neighbour), destination.TravelingCosts.From(farthestResult)))
          {
            expansionHeap.Push(neighbour);
            resultHeap.Push(neighbour);
            if (resultHeap.Buffer.Count > k)
            {
              resultHeap.Pop();
            }
          }

          // update visited list
          visited.Add(neighbour.Id);
        }
      }
    }

    return resultHeap.Buffer;
  }
}