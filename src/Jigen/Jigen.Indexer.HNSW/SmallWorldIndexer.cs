using System.Security.Cryptography;
using Jigen.DataStructures;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;

namespace Jigen.Indexer;

internal delegate IList<IndexNode> SelectForConnectingDelegate(IndexNode item, IList<IndexNode> candidates,  SmallWorldIndexer smallworld, string collection);

public class SmallWorldIndexer : IIndexer
{
  internal SmallWorldOptions Options { get; init; }
  private readonly ReaderWriterLockSlim _sync = new(LockRecursionPolicy.SupportsRecursion);
  private readonly Dictionary<string, ReaderWriterLockSlim> _collectionLocks = new(StringComparer.Ordinal);
  private readonly Dictionary<string, (IndexNode entrypoint, StoredList<IndexNode, SmallWorldOptions> nodes)> _collectionGraphs = new();
  // private readonly Dictionary<string, Dictionary<string, int>> _activeNodeByKeyByCollection = new(StringComparer.Ordinal);
  // private readonly Dictionary<string, HashSet<int>> _deletedNodeByCollection = new(StringComparer.Ordinal);

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
    _sync.EnterUpgradeableReadLock();
    try
    {
      if (_collectionGraphs.TryGetValue(collection, out var item)) return item;

      if(!Directory.Exists(Options.StoragePath)) Directory.CreateDirectory(Options.StoragePath);
      var filePath = Path.Combine(Options.StoragePath, $"{SanitizeCollectionName(collection)}.hnsw");

      var nodes = new StoredList<IndexNode, SmallWorldOptions>(new StoreListOptions()
      {
        if(!Directory.Exists(Options.StoragePath)) Directory.CreateDirectory(Options.StoragePath);
        var filePath = Path.Combine(Options.StoragePath, $"{SanitizeCollectionName(collection)}.hnsw");

      IndexNode entrypoint = null;

      for (var i = 0; i < nodes.Count; i++)
      {
        var node = nodes[i];
        var nodeKey = ToStableKey(node.Id.Value);
        
        if (entrypoint == null || node.MaxLevel > entrypoint.MaxLevel)
          entrypoint = node;
      }

      item = (entrypoint, nodes);

        return item;
      }
      finally
      {
        _sync.ExitWriteLock();
      }
    }
    finally
    {
      _sync.ExitUpgradeableReadLock();
    }
  }

  public void AddToIndex(VectorEntry entry)
  {
    if (entry is null || entry.Id is null || string.IsNullOrWhiteSpace(entry.CollectionName) || entry.Embedding.IsEmpty)
      return;

    var collection = entry.CollectionName;
    var graph = GetGraphForCollection(collection);
    var newNode = entry.ToNode(Options);
    var newNodeKey = ToStableKey(entry.Id);

    var @lock = _collectionLocks[collection];
    @lock.EnterWriteLock();
    try
    {
      var collection = entry.CollectionName;
      var graph = GetGraphForCollection(collection);
      var newNode = entry.ToNode(Options);
      
      // IMPORTANT: entrypoing is the first node. So if the graph is empty, we need to add the new node as entrypoint and return before trying to connect it to itself.
      if(graph.nodes.Count == 0) 
        graph.nodes.AddNewNode(VectorEntry.Empty.ToNode(Options));
      
      graph.nodes.AddNewNode(newNode);

      if (graph.entrypoint == null)
      {
        graph.entrypoint = newNode; 
        graph.nodes[0] = newNode; // entrypoint is always the node in position 0 ; 
        return;
      }
    }
    finally
    {
      @lock.ExitWriteLock();
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
      _sync.EnterWriteLock();
      try
      {
        graph.entrypoint = newNode;
        _collectionGraphs[collection] = graph;
      }
      finally
      {
        _sync.ExitWriteLock();
      }
    }
  }

  public void RemoveFromIndex(string collection, byte[] key)
  {
    if (string.IsNullOrWhiteSpace(collection) || key is null) return;

    _ = GetGraphForCollection(collection);

    var nodeKey = ToStableKey(key);

    _sync.EnterWriteLock();
    try
    {
      var activeNodes = _activeNodeByKeyByCollection[collection];
      var deletedNodes = _deletedNodeByCollection[collection];

      if (activeNodes.TryGetValue(nodeKey, out var activePosition))
      {
        deletedNodes.Add(activePosition);
        activeNodes.Remove(nodeKey);
      }
    }
    finally
    {
      _sync.ExitWriteLock();
    }
  }

  public List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection) || queryVector is null || queryVector.Length == 0 || top <= 0)
      return [];

    List<IndexNode> neighbours;
    var destination = CreateQueryNode(queryVector);

    var graph = GetGraphForCollection(collection);
    if (graph.entrypoint == null)
      return [];

    var searchTop = Math.Max(top, Options.SearchPruning);
    neighbours = this.KNearest(collection, destination, searchTop).ToList();

    var resultsByKey = new Dictionary<string, (VectorEntry entry, float score)>(StringComparer.Ordinal);

    foreach (var node in neighbours)
    {
      var nodeKey = ToStableKey(node.Id.Value);

      bool isActive = false;
      _sync.EnterReadLock();
      try
      {
        if (!_deletedNodeByCollection[collection].Contains(node.PositionId))
        {
          if (_activeNodeByKeyByCollection[collection].TryGetValue(nodeKey, out var activePosition) && activePosition == node.PositionId)
            isActive = true;
        }
      }
      finally
      {
        _sync.ExitReadLock();
      }

      if (!isActive) continue;

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
    _sync.EnterReadLock();
    try
    {
      if (!_deletedNodeByCollection.TryGetValue(collection, out var deletedNodes))
        return false;

      return deletedNodes.Contains(positionId);
    }
    finally
    {
      _sync.ExitReadLock();
    }
  }

  public Task FlushAsync()
  {
    List<StoredList<IndexNode, SmallWorldOptions>> toFlush;
    _sync.EnterReadLock();
    try
    {
      toFlush = _collectionGraphs.Values.Select(g => g.nodes).ToList();
    }
    finally
    {
      _sync.ExitReadLock();
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