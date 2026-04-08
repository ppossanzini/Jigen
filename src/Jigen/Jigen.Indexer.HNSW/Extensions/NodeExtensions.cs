using System.Runtime.CompilerServices;
using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Indexer.Extensions;

public static class NodeExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetM( int m, int level)
  {
    return level == 0 ? 2 * m : m;
  }

  public static void AddNewNode(this StoredList<IndexNode> nodes, IndexNode node)
  {
    lock (nodes)
    {
      node.PositionId = nodes.Count;
      nodes.Add(node);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetMaxLevel(SmallWorldOptions options)
  {
    var r = -Math.Log(options.generator.NextDouble()) * (1 / options.LevelLambda);
    return (int)r;
  }

  public static IndexNode ToNode(this VectorEntry item, SmallWorldOptions options)
  {
    var maxLevel = GetMaxLevel(options);
    var node = new IndexNode(options)
    {
      Connections = new List<IList<int>>(maxLevel + 1),
      MaxLevel = maxLevel, Id = item.Id
    };

    for (int level = 0; level <= maxLevel; ++level)
      node.Connections.Add(new List<int>(GetM(options.M, level)));

    return node;
  }


  public static void AddConnection(this IndexNode item, IndexNode newNeighbour, int level, SmallWorld smallworld)
  {
    var levelNeighbours = item.Connections[level];
    levelNeighbours.Add(newNeighbour.PositionId);
    if (levelNeighbours.Count > GetM(smallworld.Options.M, level))
    {
      item.Connections[level] = smallworld.SelectBestForConnecting(item, item.GetConnections(level, smallworld).ToList(), smallworld).Select(i => i.PositionId)
        .ToList();
    }
  }

  public static IList<IndexNode> SelectBestForConnectingAlg3(this IndexNode item, IList<IndexNode> candidates, SmallWorld smallworld)
  {
    /*
     * q ← this
     * return M nearest elements from C to q
     */

    IComparer<IndexNode> fartherIsLess = item.TravelingCosts.Reverse();
    var candidatesHeap = new BinaryHeap<IndexNode>(candidates, fartherIsLess);

    var result = new List<IndexNode>(GetM(smallworld.Options.M, item.MaxLevel) + 1);
    while (candidatesHeap.Buffer.Any() && result.Count < GetM(smallworld.Options.M, item.MaxLevel))
    {
      result.Add(candidatesHeap.Pop());
    }

    return result;
  }


  public static IList<IndexNode> SelectBestForConnectingAlg4(this IndexNode item, IList<IndexNode> candidates,  SmallWorld smallworld)
  {
    /*
     * q ← this
     * R ← ∅    // result
     * W ← C    // working queue for the candidates
     * if expandCandidates  // expand candidates
     *   for each e ∈ C
     *     for each eadj ∈ neighbourhood(e) at layer lc
     *       if eadj ∉ W
     *         W ← W ⋃ eadj
     *
     * Wd ← ∅ // queue for the discarded candidates
     * while │W│ gt 0 and │R│ lt M
     *   e ← extract nearest element from W to q
     *   if e is closer to q compared to any element from R
     *     R ← R ⋃ e
     *   else
     *     Wd ← Wd ⋃ e
     *
     * if keepPrunedConnections // add some of the discarded connections from Wd
     *   while │Wd│ gt 0 and │R│ lt M
     *   R ← R ⋃ extract nearest element from Wd to q
     *
     * return R
     */

    IComparer<IndexNode> closerIsLess = item.TravelingCosts;
    IComparer<IndexNode> fartherIsLess = closerIsLess.Reverse();

    var resultHeap = new BinaryHeap<IndexNode>(new List<IndexNode>(GetM(smallworld.Options.M, item.MaxLevel) + 1), closerIsLess);
    var candidatesHeap = new BinaryHeap<IndexNode>(candidates, fartherIsLess);

    // expand candidates option is enabled
    if (smallworld.Options.ExpandBestSelection)
    {
      var candidatesIds = new HashSet<VectorKey>(candidates.Select(c => c.Id));
      foreach (var neighbour in item.GetConnections(item.MaxLevel, smallworld))
      {
        if (candidatesIds.Contains(neighbour.Id)) continue;
        candidatesHeap.Push(neighbour);
        candidatesIds.Add(neighbour.Id);
      }
    }

    // main stage of moving candidates to result
    var discardedHeap = new BinaryHeap<IndexNode>(new List<IndexNode>(candidatesHeap.Buffer.Count), fartherIsLess);
    while (candidatesHeap.Buffer.Any() && resultHeap.Buffer.Count < GetM(smallworld.Options.M, item.MaxLevel))
    {
      var candidate = candidatesHeap.Pop();
      var farestResult = resultHeap.Buffer.FirstOrDefault();

      if (farestResult == null || Tools.DLt(item.TravelingCosts.From(candidate), item.TravelingCosts.From(farestResult)))
      {
        resultHeap.Push(candidate);
      }
      else if (smallworld.Options.KeepPrunedConnections)
      {
        discardedHeap.Push(candidate);
      }
    }

    // keep pruned option is enabled
    if (!smallworld.Options.KeepPrunedConnections) return resultHeap.Buffer;

    while (discardedHeap.Buffer.Any() && resultHeap.Buffer.Count < GetM(smallworld.Options.M, item.MaxLevel))
      resultHeap.Push(discardedHeap.Pop());


    return resultHeap.Buffer;
  }


  public static IEnumerable<IndexNode> GetConnections(this IndexNode node, int level, SmallWorld smallworld)
  {
    if (level >= node.Connections.Count) yield break;
    foreach (var idx in node.Connections[level])
      yield return smallworld.Nodes[idx];
  }
}