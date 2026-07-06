// SEARCH-LAYER hot-path optimizations:
//  - BinaryHeap<T> now uses a T[] backing store: no IList dispatch, no virtual calls.
//  - Visited set: HashSet<int> replaced with an ArrayPool<bool> direct-indexed array.
//    bool[] lookup is O(1) with a single branch vs HashSet hash+bucket traversal.
//  - GetConnections overload that takes the pre-fetched graph to avoid repeated
//    internal dictionary lookups per neighbour.
//  - All LINQ calls (.Any(), .First()) eliminated from the hot loop.

using System.Buffers;
using Jigen.DataStructures;

namespace Jigen.Indexer.Extensions;

public static class SmallWorldExtensions
{
  /// <summary>
  /// Implementation of SEARCH-LAYER(q, ep, ef, lc) — Article Section 4, Algorithm 2.
  /// </summary>
  public static IList<IndexNode> KNearestAtLevel(
    this SmallWorldIndexer smallworld,
    string collection,
    IndexNode entryPoint,
    IndexNode destination,
    int k,
    int level)
  {
    /*
     * v ← ep
     * C ← ep  (expansion candidates, ordered by distance ASC — farthest is least useful)
     * W ← ep  (result window,        ordered by distance DESC — farthest = Peek)
     * while │C│ > 0
     *   c ← extract nearest from C to q
     *   f ← get furthest from W to q
     *   if distance(c, q) > distance(f, q)  → all of W is already better than remaining C
     *     break
     *   for each e ∈ neighbourhood(c) at lc
     *     if e ∉ v
     *       v ← v ∪ e
     *       f ← get furthest from W to q
     *       if distance(e, q) < distance(f, q) or │W│ < ef
     *         C ← C ∪ e
     *         W ← W ∪ e
     *         if │W│ > ef  → remove furthest from W
     * return W
     */

    var travelingCosts = destination.TravelingCosts;
    IComparer<IndexNode> fartherIsLess = travelingCosts.Reverse();

    // Heaps: result (max-heap by distance → Peek() = farthest),
    //        expansion (max-heap with reversed comparator → Peek() = nearest).
    var resultHeap    = new BinaryHeap<IndexNode>(travelingCosts, k + 1);
    var expansionHeap = new BinaryHeap<IndexNode>(fartherIsLess, 16);

    // Deleted nodes stay navigable (their links bridge the graph) but must
    // never surface in the result window.
    if (!entryPoint.IsDeleted)
      resultHeap.Push(entryPoint);
    expansionHeap.Push(entryPoint);

    // Pre-fetch graph once; also used by the inner GetConnections overload to skip
    // the repeated internal dictionary lookup per neighbour.
    var graph     = smallworld.GetGraphForCollection(collection);
    int nodeCount = graph.nodes.Count;

    // Visited array from pool: direct bool[] indexing beats HashSet for dense integer keys.
    // We rent at least nodeCount+1 to handle any node that was freshly inserted.
    int visitedSize = Math.Max(nodeCount + 1, 1);
    bool[] visitedArr = ArrayPool<bool>.Shared.Rent(visitedSize);
    visitedArr.AsSpan(0, visitedSize).Clear();
    visitedArr[entryPoint.PositionId] = true;

    try
    {
      while (!expansionHeap.IsEmpty)
      {
        var toExpand = expansionHeap.Pop();

        // Stop only once the result window is full: with deletions the window
        // can be short of k while nearer live nodes are still reachable
        // through deleted ones. Without deletions this matches the canonical
        // break (candidates are always accepted into the window while |W| < ef).
        if (resultHeap.Count >= k
            && Tools.DGt(travelingCosts.From(toExpand), travelingCosts.From(resultHeap.Peek())))
          break; // every remaining candidate is farther than the worst result

        foreach (var neighbour in toExpand.GetConnections(level, graph))
        {
          int nid = neighbour.PositionId;

          // Bounds-safe: nodes inserted concurrently may have PositionId >= visitedSize.
          // Those nodes are not yet fully wired and can safely be skipped.
          if ((uint)nid >= (uint)visitedSize || visitedArr[nid])
            continue;

          visitedArr[nid] = true;

          if (resultHeap.Count < k
              || Tools.DLt(travelingCosts.From(neighbour), travelingCosts.From(resultHeap.Peek())))
          {
            expansionHeap.Push(neighbour);

            // Deleted nodes are expanded but never returned.
            if (!neighbour.IsDeleted)
            {
              resultHeap.Push(neighbour);
              if (resultHeap.Count > k)
                resultHeap.Pop();
            }
          }
        }
      }
    }
    finally
    {
      ArrayPool<bool>.Shared.Return(visitedArr);
    }

    return resultHeap.ToList();
  }
}
