using System.Collections.Concurrent;
using Jigen.DataStructures;

namespace Jigen.Indexer;

public partial class SmallWorldIndexer
{
  // GetIndexInfo is polled by the metrics sampler every 5s for every collection:
  // cache the O(n) scan.
  private readonly ConcurrentDictionary<string, (DateTime computedAt, CollectionIndexInfo info)> _indexInfoCache = new();
  private static readonly TimeSpan IndexInfoTtl = TimeSpan.FromSeconds(5);

  private string GraphBasePathFor(string collection) =>
    Path.Combine(Options.StoragePath ?? string.Empty, $"{SanitizeCollectionName(collection)}.hnsw");

  /// <summary>True when a graph exists WITHOUT creating one (GetGraphForCollection
  /// creates files as a side effect, which stats must never do).</summary>
  private bool GraphExistsFor(string collection)
  {
    if (_collectionGraphs.ContainsKey(collection)) return true;
    if (Options.InMemory) return false;
    var basePath = GraphBasePathFor(collection);
    return File.Exists($"{basePath}.vec") || File.Exists(basePath); // split files or legacy single file
  }

  private long IndexFilesSizeFor(string collection)
  {
    if (Options.InMemory) return 0;
    var basePath = GraphBasePathFor(collection);
    long size = 0;
    foreach (var path in new[] { $"{basePath}.vec", $"{basePath}.adj", basePath })
    {
      var fi = new FileInfo(path);
      if (fi.Exists) size += fi.Length;
    }
    return size;
  }

  public CollectionIndexInfo GetIndexInfo(string collection)
  {
    var empty = new CollectionIndexInfo { Quantization = Options.Quantization.ToString() };
    if (string.IsNullOrWhiteSpace(collection) || !GraphExistsFor(collection)) return empty;

    if (_indexInfoCache.TryGetValue(collection, out var cached) &&
        DateTime.UtcNow - cached.computedAt < IndexInfoTtl)
      return cached.info;

    var graph = GetGraphForCollection(collection);
    var info = new CollectionIndexInfo { Quantization = Options.Quantization.ToString() };
    var perLevel = new List<int>();
    long degreeSum = 0;

    // Graph lock: prevents node allocation/removal while scanning (adjacency
    // COUNTS may still move under per-node wiring; values are approximate).
    lock (graph.nodes)
    {
      for (var i = 1; i < graph.nodes.Count; i++)
      {
        var node = graph.nodes[i];
        if (node.IsDeleted) { info.DeletedNodes++; continue; }
        if (node.VectorDimensions == 0) continue; // defensive: placeholder-like slot
        info.Nodes++;
        var lvl = node.MaxLevel;
        if (lvl > info.MaxLevel) info.MaxLevel = lvl;
        while (perLevel.Count <= lvl) perLevel.Add(0);
        perLevel[lvl]++;
        degreeSum += node.Connections.Count > 0 ? node.Connections[0]?.Count ?? 0 : 0;
      }
    }

    info.NodesPerLevel = perLevel.ToArray();
    info.AverageDegree = info.Nodes > 0 ? Math.Round((double)degreeSum / info.Nodes, 2) : 0;
    info.IndexSizeBytes = IndexFilesSizeFor(collection);

    _indexInfoCache[collection] = (DateTime.UtcNow, info);
    return info;
  }

  public IndexGraphSnapshot GetGraphSnapshot(string collection, int dimensions = 2, int limit = 2000, int? level = null, float[] queryVector = null)
  {
    dimensions = Math.Clamp(dimensions, 2, 3);
    limit = Math.Clamp(limit, 1, 20_000);
    if (level is < 0) level = null;

    var snapshot = new IndexGraphSnapshot { Collection = collection, Dimensions = dimensions };
    if (string.IsNullOrWhiteSpace(collection) || !GraphExistsFor(collection)) return snapshot;

    var graph = GetGraphForCollection(collection);

    // Raw per-node copies taken under the graph lock; DTOs and PCA are built after release.
    var sampled = new List<(int pos, byte[] key, int maxLevel, bool isDeleted, int[][] connections, float[] vector)>();
    var eligibleCount = 0;

    lock (graph.nodes)
    {
      graph = GetGraphForCollection(collection); // refresh entrypoint under lock (same idiom as AddToIndex)
      var count = graph.nodes.Count;
      snapshot.TotalNodes = Math.Max(0, count - 1);
      snapshot.EntrypointPositionId = graph.entrypoint?.PositionId ?? 0;

      bool Eligible(IndexNode n) => n.VectorDimensions > 0 && (level is null || n.MaxLevel >= level.Value);

      // Aggregate counters (single full pass over the RAM-resident canonical nodes).
      for (var i = 1; i < count; i++)
      {
        var n = graph.nodes[i];
        if (n.IsDeleted) snapshot.DeletedNodes++;
        else if (n.VectorDimensions > 0) snapshot.LiveNodes++;
        if (n.MaxLevel > snapshot.MaxLevel) snapshot.MaxLevel = n.MaxLevel;
        if (Eligible(n)) eligibleCount++;
      }

      // Copies one node's data; adjacency is copied under lock(node) because
      // concurrent inserts mutate Connections under per-node locks only.
      // Lock order graph.nodes -> node matches the rest of the indexer.
      void CopyNode(IndexNode node)
      {
        int[][] conns;
        lock (node)
        {
          var src = node.Connections;
          if (level is int filtered)
          {
            var levelConns = src.Count > filtered ? src[filtered] : null;
            conns = [levelConns?.ToArray() ?? []];
          }
          else
          {
            conns = new int[src.Count][];
            for (var l = 0; l < src.Count; l++) conns[l] = src[l]?.ToArray() ?? [];
          }
        }
        // Vector getter: immutable payload, safe outside the node lock; dequantizes SQ8.
        sampled.Add((node.PositionId, node.Id.Value ?? [], node.MaxLevel, node.IsDeleted, conns, node.Vector));
      }

      // --- Sampling: BFS from the entrypoint (top of the navigation structure), ---
      // --- then a linear top-up for disconnected leftovers.                     ---
      var visited = new HashSet<int>();
      var queue = new Queue<int>();
      var entryPos = graph.entrypoint?.PositionId ?? 0;
      if (entryPos > 0 && entryPos < count && Eligible(graph.nodes[entryPos]))
      {
        visited.Add(entryPos);
        queue.Enqueue(entryPos);
      }

      while (queue.Count > 0 && sampled.Count < limit)
      {
        var pos = queue.Dequeue();
        CopyNode(graph.nodes[pos]);
        foreach (var levelConnections in sampled[^1].connections)
          foreach (var neighbour in levelConnections)
          {
            if (neighbour <= 0 || neighbour >= count || !visited.Add(neighbour)) continue;
            if (!Eligible(graph.nodes[neighbour])) { visited.Remove(neighbour); continue; }
            queue.Enqueue(neighbour);
          }
      }

      for (var i = 1; i < count && sampled.Count < limit; i++)
      {
        if (visited.Contains(i)) continue;
        var node = graph.nodes[i];
        if (!Eligible(node)) continue;
        visited.Add(i);
        CopyNode(node);
      }
    } // release graph lock

    snapshot.ReturnedNodes = sampled.Count;
    snapshot.Truncated = sampled.Count < eligibleCount;
    if (sampled.Count == 0) return snapshot;

    // --- PCA projection (outside all locks) ---
    // The query vector (if any) rides along as an extra row so it lands in the SAME
    // basis as the sampled nodes; it is never added to `sampled`/nodes/edges since it
    // is synthetic, not a real graph node. Its projected row is the last one.
    var vectors = sampled.Select(s => s.vector).ToList();
    var hasQueryVector = queryVector is { Length: > 0 };
    if (hasQueryVector) vectors.Add(queryVector);

    var coords = PcaProjection.Project(vectors, dimensions);
    if (hasQueryVector) snapshot.QueryPosition = coords[^1];

    var positionToIndex = new Dictionary<int, int>(sampled.Count);
    for (var i = 0; i < sampled.Count; i++) positionToIndex[sampled[i].pos] = i;

    var nodes = new List<IndexGraphNode>(sampled.Count);
    for (var i = 0; i < sampled.Count; i++)
    {
      var s = sampled[i];
      nodes.Add(new IndexGraphNode
      {
        PositionId = s.pos,
        Key = Convert.ToBase64String(s.key),
        MaxLevel = s.maxLevel,
        IsDeleted = s.isDeleted,
        Degree = s.connections.Length > 0 ? s.connections[0].Length : 0,
        Position = coords[i]
      });
    }

    // Edges: undirected dedupe per (min,max,level); only edges whose both ends were sampled.
    var seen = new HashSet<(int a, int b, int lvl)>();
    var edges = new List<IndexGraphEdge>();
    foreach (var s in sampled)
      for (var l = 0; l < s.connections.Length; l++)
      {
        var edgeLevel = level ?? l; // with a level filter connections[0] IS that layer
        foreach (var neighbour in s.connections[l])
        {
          if (neighbour == s.pos || !positionToIndex.ContainsKey(neighbour)) continue;
          var a = Math.Min(s.pos, neighbour);
          var b = Math.Max(s.pos, neighbour);
          if (seen.Add((a, b, edgeLevel)))
            edges.Add(new IndexGraphEdge { Source = a, Target = b, Level = edgeLevel });
        }
      }

    snapshot.Nodes = nodes;
    snapshot.Edges = edges;
    return snapshot;
  }
}
