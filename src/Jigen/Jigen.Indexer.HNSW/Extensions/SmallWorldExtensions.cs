// SEARCH-LAYER hot-path optimizations:
//  - BinaryHeap<T> now uses a T[] backing store: no IList dispatch, no virtual calls.
//  - Visited set: pooled epoch-stamped int[] (VisitedSet) — starting a traversal
//    is a counter bump instead of clearing nodeCount bytes per level.
//  - GetConnections overload that takes the pre-fetched graph to avoid repeated
//    internal dictionary lookups per neighbour.
//  - All LINQ calls (.Any(), .First()) eliminated from the hot loop.

using Jigen.DataStructures;

namespace Jigen.Indexer.Extensions;

public static class SmallWorldExtensions
{
  /// <summary>
  /// Implementation of SEARCH-LAYER(q, ep, ef, lc) — Article Section 4, Algorithm 2.
  /// </summary>
  /// <param name="accept">
  /// Optional ACORN-1-style metadata filter: a candidate must satisfy it to
  /// enter the result window, but — like a deleted node — is still expanded,
  /// so filtered-out nodes keep bridging the graph instead of fragmenting it.
  /// Null runs the plain unfiltered search (construction, level &gt; 0 descent).
  /// </param>
  public static IList<IndexNode> KNearestAtLevel(
    this SmallWorldIndexer smallworld,
    string collection,
    IndexNode entryPoint,
    IndexNode destination,
    int k,
    int level,
    Func<IndexNode, bool> accept = null)
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
    // never surface in the result window — same treatment for nodes the
    // caller's filter rejects (accept == null accepts everything, the
    // pre-filter behaviour used by construction and unfiltered search).
    bool EntersResults(IndexNode node) => !node.IsDeleted && (accept is null || accept(node));

    if (EntersResults(entryPoint))
      resultHeap.Push(entryPoint);
    expansionHeap.Push(entryPoint);

    // Pre-fetch graph once; also used by the inner GetConnections overload to skip
    // the repeated internal dictionary lookup per neighbour.
    var graph     = smallworld.GetGraphForCollection(collection);
    int nodeCount = graph.nodes.Count;

    // Epoch-stamped visited set from the indexer pool: starting a traversal
    // is a counter bump, not an O(nodeCount) clear per level per operation.
    var visited = smallworld.RentVisitedSet(nodeCount + 1);
    visited.TryVisit(entryPoint.PositionId);

    // A selective filter can leave the result window short of k for a long
    // time (most candidates get rejected by `accept`, not just skipped like
    // deletions): bound the expansion so a near-always-false filter degrades
    // to a partial scan instead of visiting the whole graph every query.
    // Unfiltered searches (accept == null) keep the original, unbounded loop.
    var expansionBudget = accept is null
      ? int.MaxValue
      : Math.Max(k, k * smallworld.Options.FilteredSearchExpansionFactor);
    var expanded = 0;

    try
    {
      while (!expansionHeap.IsEmpty)
      {
        var toExpand = expansionHeap.Pop();

        // Stop only once the result window is full: with deletions (or a
        // filter) the window can be short of k while nearer accepted nodes
        // are still reachable through rejected ones. Without either this
        // matches the canonical break (candidates are always accepted into
        // the window while |W| < ef).
        if (resultHeap.Count >= k
            && Tools.DGt(travelingCosts.From(toExpand), travelingCosts.From(resultHeap.Peek())))
          break; // every remaining candidate is farther than the worst result

        if (++expanded > expansionBudget)
          break; // filtered search budget spent: return what was found so far

        foreach (var neighbour in toExpand.GetConnections(level, graph))
        {
          // Already seen, or inserted concurrently (not fully wired): skip.
          if (!visited.TryVisit(neighbour.PositionId))
            continue;

          if (resultHeap.Count < k
              || Tools.DLt(travelingCosts.From(neighbour), travelingCosts.From(resultHeap.Peek())))
          {
            expansionHeap.Push(neighbour);

            // Deleted or filtered-out nodes are expanded but never returned.
            if (EntersResults(neighbour))
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
      smallworld.ReturnVisitedSet(visited);
    }

    return resultHeap.ToList();
  }
}
