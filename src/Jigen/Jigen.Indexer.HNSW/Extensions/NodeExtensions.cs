using System.Runtime.CompilerServices;
using System.Numerics.Tensors;
using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Indexer.Extensions;

public static class NodeExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetM(int m, int level) => level == 0 ? 2 * m : m;

  [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
  public static void AddNewNode(this IList<IndexNode> nodes, IndexNode node)
  {
    if (node is null) return;
    lock (nodes)
    {
      node.PositionId = nodes.Count;
      nodes.Add(node);
    }
  }

  public static int GetMaxLevel(SmallWorldOptions options)
  {
    // Random.Shared (the default) fans out to thread-local state internally,
    // so sampling it concurrently needs no lock. A caller-supplied Random
    // (e.g. seeded for reproducible tests) is NOT thread-safe: different
    // collections (and fire-and-forget AddToIndex) can reach this from
    // multiple threads, and a torn Random silently degenerates to returning
    // 0, so that path is still locked.
    var generator = options.generator;
    double sample;
    if (ReferenceEquals(generator, Random.Shared))
      sample = generator.NextDouble();
    else
      lock (generator)
        sample = generator.NextDouble();

    // 1 - sample ∈ (0, 1]: NextDouble can return exactly 0, and -log(0) = +∞
    // would turn into a negative level after the int cast.
    // Level = floor(-ln(unif) * mL), mL = LevelLambda = 1/ln(M) (article, sec. 4).
    var r = -Math.Log(1.0 - sample) * options.LevelLambda;
    return (int)r;
  }

  public static IndexNode ToNode(this VectorEntry item, SmallWorldOptions options)
  {
    var maxLevel = GetMaxLevel(options);
    var vector = item.Embedding.ToArray();
    NormalizeInPlace(vector);

    var node = new IndexNode(options)
    {
      Connections = new IList<int>[maxLevel + 1],
      MaxLevel = maxLevel, Id = item.Id, Vector = vector
    };

    // SQ8 graphs compare quantized payloads on both sides.
    if (options.Quantization == VectorQuantization.SQ8)
      node.RamQuantized = Sq8.Quantize(vector);

    for (int level = 0; level <= maxLevel; ++level)
      node.Connections[level] = new List<int>(GetM(options.M, level));

    return node;
  }

  private static void NormalizeInPlace(Span<float> vector)
  {
    if (vector.Length == 0) return;
    var norm = TensorPrimitives.Norm(vector);
    if (norm <= 0) return;
    TensorPrimitives.Divide(vector, norm, vector);
  }

  public static void AddConnection(this IndexNode item, IndexNode newNeighbour, int level, SmallWorldIndexer smallworld, string collection)
  {
    item.AddConnection(newNeighbour, level, smallworld, collection, smallworld.GetGraphForCollection(collection));
  }

  /// <summary>
  /// Fast overload for hot loops: takes the already-fetched graph tuple to
  /// skip a dictionary lookup per connection add.
  /// </summary>
  internal static void AddConnection(this IndexNode item, IndexNode newNeighbour, int level, SmallWorldIndexer smallworld, string collection,
    (IndexNode entrypoint, IList<IndexNode> nodes) graph)
  {
    var levelNeighbours = item.Connections[level];
    if (levelNeighbours is int[] or null)
    {
      levelNeighbours = levelNeighbours == null ? new List<int>() : new List<int>(levelNeighbours);
      item.Connections[level] = levelNeighbours;
    }

    levelNeighbours.Add(newNeighbour.PositionId);

    if (levelNeighbours.Count > GetM(smallworld.Options.M, level))
    {
      // Collect current connections without LINQ/ToList allocation via IEnumerable
      var currentConns = new List<IndexNode>();
      foreach (var conn in item.GetConnections(level, graph))
        currentConns.Add(conn);

      var best = smallworld.SelectBestForConnecting(item, currentConns, level, smallworld, collection);

      var newIds = new List<int>(best.Count);
      foreach (var bc in best) newIds.Add(bc.PositionId);
      item.Connections[level] = newIds;
    }
  }

  public static IList<IndexNode> SelectBestForConnectingAlg3(
    this IndexNode item,
    IList<IndexNode> candidates,
    int level,
    SmallWorldIndexer smallworld,
    string collection)
  {
    // Return M nearest elements from candidates to item (Algorithm 3).
    // M is a property of the layer being wired, not of the item's top level.
    int maxM = GetM(smallworld.Options.M, level);

    // LOCAL TravelingCosts: `item` is a shared graph node and concurrent
    // inserts prune it under its own lock while its owner compares distances
    // lock-free — sharing item.TravelingCosts here corrupts its cache.
    var costs = new TravelingCosts(item, smallworld.Options);
    IComparer<IndexNode> fartherIsLess = costs.Reverse();
    var candidatesHeap = new BinaryHeap<IndexNode>(candidates, fartherIsLess);

    var result = new List<IndexNode>(maxM + 1);
    while (!candidatesHeap.IsEmpty && result.Count < maxM)
      result.Add(candidatesHeap.Pop());

    return result;
  }

  public static IList<IndexNode> SelectBestForConnectingAlg4(
    this IndexNode item,
    IList<IndexNode> candidates,
    int level,
    SmallWorldIndexer smallworld,
    string collection)
  {
    /*
     * R ← ∅        result
     * W ← C        working queue
     * if expandCandidates: expand W with neighbours of C
     * Wd ← ∅       discarded
     * while │W│ > 0 and │R│ < M
     *   e ← extract nearest from W
     *   if e closer to q than any element of R → R ← R ∪ e
     *   else → Wd ← Wd ∪ e
     * if keepPrunedConnections: fill R from Wd until │R│ = M
     * return R
     */

    int maxM = GetM(smallworld.Options.M, level);

    // LOCAL TravelingCosts: see SelectBestForConnectingAlg3 — the shared
    // node's cache must never be touched from concurrent prunes.
    var costs = new TravelingCosts(item, smallworld.Options);
    IComparer<IndexNode> closerIsLess = costs;
    IComparer<IndexNode> fartherIsLess = closerIsLess.Reverse();

    var resultHeap    = new BinaryHeap<IndexNode>(closerIsLess, maxM + 1);
    var candidatesHeap = new BinaryHeap<IndexNode>(candidates, fartherIsLess);

    if (smallworld.Options.ExpandBestSelection)
    {
      // Add neighbours of existing candidates not already in the working set.
      var graph = smallworld.GetGraphForCollection(collection);
      var candidatesIds = new HashSet<int>(candidates.Count);
      foreach (var c in candidates) candidatesIds.Add(c.PositionId);

      foreach (var neighbour in item.GetConnections(level, graph))
      {
        if (candidatesIds.Add(neighbour.PositionId))
          candidatesHeap.Push(neighbour);
      }
    }

    var discardedHeap = new BinaryHeap<IndexNode>(fartherIsLess, candidatesHeap.Count);

    while (!candidatesHeap.IsEmpty && resultHeap.Count < maxM)
    {
      var candidate    = candidatesHeap.Pop();
      var farestResult = resultHeap.IsEmpty ? null : resultHeap.Peek();

      if (farestResult is null
          || Tools.DLt(costs.From(candidate), costs.From(farestResult)))
      {
        resultHeap.Push(candidate);
      }
      else if (smallworld.Options.KeepPrunedConnections)
      {
        discardedHeap.Push(candidate);
      }
    }

    if (!smallworld.Options.KeepPrunedConnections)
      return resultHeap.ToList();

    while (!discardedHeap.IsEmpty && resultHeap.Count < maxM)
      resultHeap.Push(discardedHeap.Pop());

    return resultHeap.ToList();
  }

  /// <summary>
  /// Iterates the connections of <paramref name="node"/> at the given level,
  /// deleted nodes included: they stay navigable (they bridge their former
  /// neighbourhoods, so skipping them fragments the graph as deletes pile up)
  /// and are filtered from search RESULTS instead (see KNearestAtLevel).
  /// Uses the public API (passes through SmallWorldIndexer) — kept for external callers.
  /// </summary>
  public static IEnumerable<IndexNode> GetConnections(
    this IndexNode node,
    int level,
    SmallWorldIndexer smallworld,
    string collection)
  {
    var graph = smallworld.GetGraphForCollection(collection);
    return node.GetConnections(level, graph);
  }

  /// <summary>
  /// Fast internal overload: accepts the already-fetched graph tuple to avoid
  /// the extra dictionary lookup per call inside hot loops.
  /// </summary>
  internal static IEnumerable<IndexNode> GetConnections(
    this IndexNode node,
    int level,
    (IndexNode ep, IList<IndexNode> nodes) graph)
  {
    if (level >= node.Connections.Count) yield break;

    // Indexed access instead of an enumerator: a search running concurrently
    // with an insert must not hit the List version check.
    var connections = node.Connections[level];
    for (var i = 0; i < connections.Count; i++)
    {
      var id = connections[i];
      // A crash can leave a persisted graph with fewer nodes than its
      // adjacency lists reference (the store clamps a torn tail on load):
      // skip dangling ids instead of throwing on the whole search/insert.
      if ((uint)id >= (uint)graph.nodes.Count) continue;

      // Deleted nodes are yielded on purpose: traversal must pass through
      // them or the graph fragments; callers filter them from results.
      yield return graph.nodes[id];
    }
  }
}
