using Jigen.DataStructures;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;

namespace Jigen.Indexer;

internal delegate IList<IndexNode> SelectForConnectingDelegate(IndexNode item, IList<IndexNode> candidates,  SmallWorldIndexer smallworld, string collection);

public class SmallWorldIndexer : IIndexer
{
  internal SmallWorldOptions Options { get; init; }
  private readonly object _sync = new();
  private readonly Dictionary<string, (IndexNode entrypoint, StoredList<IndexNode, SmallWorldOptions> nodes)> _collectionGraphs = new();
  private readonly Dictionary<string, Dictionary<string, int>> _activeNodeByKeyByCollection = new(StringComparer.Ordinal);
  private readonly Dictionary<string, HashSet<int>> _deletedNodeByCollection = new(StringComparer.Ordinal);

  internal readonly SelectForConnectingDelegate SelectBestForConnecting = null;


  public SmallWorldIndexer(SmallWorldOptions options = null)
  {
    this.Options = options ?? new SmallWorldOptions();
    this.Options.DefaultDistanceFunction ??= DefaultDistance;

    this.SelectBestForConnecting = this.Options.NeighbourHeuristic switch
    {
      NeighbourSelectionHeuristic.SelectHeuristic => NodeExtensions.SelectBestForConnectingAlg4,
      NeighbourSelectionHeuristic.SelectSimple => NodeExtensions.SelectBestForConnectingAlg3,
      _ => NodeExtensions.SelectBestForConnectingAlg3
    };
  }

  internal (IndexNode entrypoint, StoredList<IndexNode, SmallWorldOptions> nodes) GetGraphForCollection(string collection)
  {
    lock (_sync)
    {
      if (_collectionGraphs.TryGetValue(collection, out var item)) return item;

      Directory.CreateDirectory(Options.StoragePath);
      var filePath = Path.Combine(Options.StoragePath, $"{SanitizeCollectionName(collection)}.hnsw");

      var nodes = new StoredList<IndexNode, SmallWorldOptions>(new StoreListOptions()
      {
        FilePath = filePath,
        FlushInterval = TimeSpan.FromMinutes(1)
      }, Options);

      IndexNode entrypoint = null;
      var activeNodes = new Dictionary<string, int>(StringComparer.Ordinal);
      var deletedNodes = new HashSet<int>();

      for (var i = 0; i < nodes.Count; i++)
      {
        var node = nodes[i];
        var nodeKey = ToStableKey(node.Id.Value);

        if (activeNodes.TryGetValue(nodeKey, out var previousPosition))
          deletedNodes.Add(previousPosition);

        activeNodes[nodeKey] = node.PositionId;

        if (entrypoint == null || node.MaxLevel > entrypoint.MaxLevel)
          entrypoint = node;
      }

      item = (entrypoint, nodes);
      _collectionGraphs[collection] = item;
      _activeNodeByKeyByCollection[collection] = activeNodes;
      _deletedNodeByCollection[collection] = deletedNodes;

      return item;
    }
  }

  public void AddToIndex(VectorEntry entry)
  {
    if (entry is null || entry.Id is null || string.IsNullOrWhiteSpace(entry.CollectionName) || entry.Embedding.IsEmpty)
      return;

    lock (_sync)
    {
      var collection = entry.CollectionName;
      var graph = GetGraphForCollection(collection);
      var newNode = entry.ToNode(Options);

      var activeNodes = _activeNodeByKeyByCollection[collection];
      var deletedNodes = _deletedNodeByCollection[collection];

      var newNodeKey = ToStableKey(entry.Id);
      if (activeNodes.TryGetValue(newNodeKey, out var previousNodePosition))
        deletedNodes.Add(previousNodePosition);

      graph.nodes.AddNewNode(newNode);
      activeNodes[newNodeKey] = newNode.PositionId;

      if (graph.entrypoint == null)
      {
        graph.entrypoint = newNode;
        _collectionGraphs[collection] = graph;
        return;
      }

      var bestPeer = graph.entrypoint;
      for (var level = bestPeer.MaxLevel; level > newNode.MaxLevel; --level)
        bestPeer = this.KNearestAtLevel(collection, bestPeer, newNode, 1, level).Single();

      for (var level = Math.Min(newNode.MaxLevel, graph.entrypoint.MaxLevel); level >= 0; --level)
      {
        var potentialNeighbours = this.KNearestAtLevel(collection, bestPeer, newNode, Options.ConstructionPruning, level);
        var bestNeighbours = SelectBestForConnecting(newNode, potentialNeighbours, this, collection);

        foreach (var newNeighbour in bestNeighbours)
        {
          newNode.AddConnection(newNeighbour, level, this, collection);
          newNeighbour.AddConnection(newNode, level, this, collection);
          graph.nodes[newNeighbour.PositionId] = newNeighbour;

          // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
          if (Tools.DLt(newNode.TravelingCosts.From(newNeighbour), newNode.TravelingCosts.From(bestPeer)))
            bestPeer = newNeighbour;
        }
      }

      // Persist the newly constructed adjacency lists for the inserted node.
      graph.nodes[newNode.PositionId] = newNode;

      // zoom out to the highest level
      if (newNode.MaxLevel > graph.entrypoint.MaxLevel)
      {
        graph.entrypoint = newNode;
        _collectionGraphs[collection] = graph;
      }
    }
  }

  public void RemoveFromIndex(string collection, byte[] key)
  {
    if (string.IsNullOrWhiteSpace(collection) || key is null) return;

    lock (_sync)
    {
      _ = GetGraphForCollection(collection);

      var nodeKey = ToStableKey(key);
      var activeNodes = _activeNodeByKeyByCollection[collection];
      var deletedNodes = _deletedNodeByCollection[collection];

      if (activeNodes.TryGetValue(nodeKey, out var activePosition))
      {
        deletedNodes.Add(activePosition);
        activeNodes.Remove(nodeKey);
      }
    }
  }

  public List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection) || queryVector is null || queryVector.Length == 0 || top <= 0)
      return [];

    List<IndexNode> neighbours;
    var destination = CreateQueryNode(queryVector);

    lock (_sync)
    {
      var graph = GetGraphForCollection(collection);
      if (graph.entrypoint == null)
        return [];

      var searchTop = Math.Max(top, Options.SearchPruning);
      neighbours = this.KNearest(collection, destination, searchTop).ToList();
    }

    var resultsByKey = new Dictionary<string, (VectorEntry entry, float score)>(StringComparer.Ordinal);

    foreach (var node in neighbours)
    {
      var nodeKey = ToStableKey(node.Id.Value);

      lock (_sync)
      {
        if (_deletedNodeByCollection[collection].Contains(node.PositionId)) continue;
        if (!_activeNodeByKeyByCollection[collection].TryGetValue(nodeKey, out var activePosition) || activePosition != node.PositionId)
          continue;
      }

      var score = 1f - destination.TravelingCosts.From(node, usecache: false);

      if (!resultsByKey.TryGetValue(nodeKey, out var existing) || score > existing.score)
      {
        var content = store.GetContent(collection, node.Id.Value);
        if (content is null)
          continue;

        resultsByKey[nodeKey] =
          (new VectorEntry { Id = node.Id.Value, CollectionName = collection, Content = content }, score);
      }
    }

    return resultsByKey.Values
      .OrderByDescending(r => r.score)
      .Take(top)
      .ToList();
  }

  /// <summary>
  /// Run knn search for a given item.
  /// </summary>
  /// <param name="item">The item to search nearest neighbours.</param>
  /// <param name="k">The number of nearest neighbours.</param>
  /// <returns>The list of found nearest neighbours.</returns>
  public IList<KNNSearchResult> KNNSearch(string collection, IndexNode item, int k)
  {
    var neighbourhood = KNearest(collection, item, k);
    return neighbourhood.Select(n => new KNNSearchResult
    {
      Id = n.PositionId,
      Item = n,
      Distance = item.TravelingCosts.From(n),
    }).ToList();
  }

  /// <summary>
  /// Get k nearest items for a given one.
  /// Contains implementation of K-NN-SEARCH(hnsw, q, K, ef) algorithm.
  /// Article: Section 4. Algorithm 5.
  /// </summary>
  /// <param name="destination">The given node to get the nearest neighbourhood for.</param>
  /// <param name="k">The size of the neighbourhood.</param>
  /// <returns>The list of the nearest neighbours.</returns>
  public IList<IndexNode> KNearest(string collection, IndexNode destination, int k)
  {
    var graph = GetGraphForCollection(collection);
    var entrypoint = graph.entrypoint;
    if (entrypoint == null) return [];

    var bestPeer = entrypoint;
    for (int level = entrypoint.MaxLevel; level > 0; --level)
    {
      bestPeer = this.KNearestAtLevel(collection, bestPeer, destination, 1, level).Single();
    }

    return this.KNearestAtLevel(collection, bestPeer, destination, k, 0);
  }

  internal bool IsDeleted(string collection, int positionId)
  {
    lock (_sync)
    {
      if (!_deletedNodeByCollection.TryGetValue(collection, out var deletedNodes))
        return false;

      return deletedNodes.Contains(positionId);
    }
  }

  public Task FlushAsync()
  {
    List<StoredList<IndexNode, SmallWorldOptions>> toFlush;
    lock (_sync)
    {
      toFlush = _collectionGraphs.Values.Select(g => g.nodes).ToList();
    }

    foreach (var nodes in toFlush)
      nodes.Flush();

    return Task.CompletedTask;
  }

  private static float DefaultDistance(IndexNode left, IndexNode right)
  {
    if (left.Vector is null || right.Vector is null || left.Vector.Length == 0 || right.Vector.Length == 0)
      return float.MaxValue;

    return CosineDistance.SIMDForUnits(left.Vector, right.Vector);
  }

  private static string ToStableKey(byte[] key)
  {
    return Convert.ToBase64String(key);
  }

  private static string SanitizeCollectionName(string collection)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var buffer = collection.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
    return new string(buffer);
  }

  private IndexNode CreateQueryNode(float[] queryVector)
  {
    var vector = queryVector.ToArray();
    NormalizeInPlace(vector);

    return new IndexNode(Options)
    {
      Id = Guid.NewGuid().ToByteArray(),
      MaxLevel = 0,
      Connections = new List<IList<int>> { new List<int>() },
      Vector = vector
    };
  }

  private static void NormalizeInPlace(float[] vector)
  {
    if (vector.Length == 0) return;

    float norm = 0;
    for (int i = 0; i < vector.Length; i++)
      norm += vector[i] * vector[i];

    if (norm <= 0) return;

    var inv = 1f / MathF.Sqrt(norm);
    for (int i = 0; i < vector.Length; i++)
      vector[i] *= inv;
  }
}