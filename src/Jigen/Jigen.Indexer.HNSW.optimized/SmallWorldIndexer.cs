using System.Collections.Concurrent;
using System.Text.Json;
using Jigen.DataStructures;
using Jigen.Filtering;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;
using System.Numerics.Tensors;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Indexer;

internal delegate IList<IndexNode> SelectForConnectingDelegate(IndexNode item, IList<IndexNode> candidates, int level, SmallWorldIndexer smallworld, string collection);

public class SmallWorldIndexer : IIndexer
{
  internal SmallWorldOptions Options { get; init; }

  private readonly ConcurrentDictionary<string, (IndexNode entrypoint, IList<IndexNode> nodes)> _collectionGraphs = new();
  private readonly Lock _graphCreationLock = new();

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

  internal (IndexNode entrypoint, IList<IndexNode> nodes) GetGraphForCollection(string collection)
  {
    if (_collectionGraphs.TryGetValue(collection, out var item)) return item;

    lock (_graphCreationLock)
    {
      if (_collectionGraphs.TryGetValue(collection, out item)) return item;

      if (!Directory.Exists(Options.StoragePath)) Directory.CreateDirectory(Options.StoragePath);
      var filePath = Path.Combine(Options.StoragePath, $"{SanitizeCollectionName(collection)}.hnsw");

      IList<IndexNode> nodes;
      if (Options.InMemory)
        nodes  = new List<IndexNode>();
      else
        nodes = new StoredList<IndexNode, SmallWorldOptions>(new StoreListOptions()
        {
          FilePath = filePath,
          FlushInterval = TimeSpan.FromMinutes(1)
        }, Options);

      if (!nodes.Any())
      {
        var entrypoint = VectorEntry.Empty.ToNode(Options);
        nodes.Add(entrypoint); // position 0 is reserved for entrypoint nodes
      }

      item = (nodes[nodes[0].PositionId], nodes);
      _collectionGraphs[collection] = item;
      return item;
    }
  }

  private void AssignEntryPoint(string collection, (IndexNode entrypoint, IList<IndexNode> nodes) entry, IndexNode newNode)
  {
    // Slot 0 stores the entrypoint pointer (resolved via PositionId on reload).
    // The dictionary value must be replaced too: tuples are value types, so
    // mutating the local copy would leave the cached entrypoint stale.
    entry.nodes[0] = newNode;
    _collectionGraphs[collection] = (newNode, entry.nodes);
  }

  public void AddToIndex(VectorEntry entry, bool  waitForIndexing = false)
  {
    if (waitForIndexing) AddToIndex(entry);
    else _ = Task.Run(() => AddToIndex(entry));
  }

  internal void AddToIndex(VectorEntry entry)
  {
    if (entry is null || entry.Id is null || string.IsNullOrWhiteSpace(entry.CollectionName) || entry.Embedding.IsEmpty)
      return;

    var collection = entry.CollectionName;
    var graph = GetGraphForCollection(collection);
    var newNode = entry.ToNode(Options);

    // Graph construction mutates shared adjacency lists: inserts must be
    // serialized per collection (the nodes list reference is stable).
    lock (graph.nodes)
    {
      graph = GetGraphForCollection(collection); // refresh entrypoint under lock

      graph.nodes.AddNewNode(newNode);

      // The initial slot-0 placeholder has an empty vector (distance = MaxValue):
      // promote the first real node to entrypoint so the placeholder never
      // becomes part of the graph.
      if (graph.entrypoint is null || graph.entrypoint.Vector.Length == 0)
      {
        AssignEntryPoint(collection, graph, newNode);
        return;
      }

      var bestPeer = graph.entrypoint;
      for (var level = bestPeer.MaxLevel; level > newNode.MaxLevel; --level)
        bestPeer = this.KNearestAtLevel(collection, bestPeer, newNode, 1, level).Single();

      for (var level = Math.Min(newNode.MaxLevel, graph.entrypoint.MaxLevel); level >= 0; --level)
      {
        var potentialNeighbours = this.KNearestAtLevel(collection, bestPeer, newNode, Options.ConstructionPruning, level);
        var bestNeighbours = SelectBestForConnecting(newNode, potentialNeighbours, level, this, collection);

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
        AssignEntryPoint(collection, graph, newNode);
    }
  }

  public void RemoveFromIndex(string collection, byte[] key)
  {
    if (string.IsNullOrWhiteSpace(collection) || key is null) return;

    var graph = GetGraphForCollection(collection);
    lock (graph.nodes)
    {
      // Skip slot 0: it aliases the entrypoint node, which is re-visited at its own PositionId.
      for (var i = 1; i < graph.nodes.Count; i++)
      {
        var node = graph.nodes[i];
        if (node.IsDeleted || node.Id.Value is null || !node.Id.Value.SequenceEqual(key)) continue;

        node.IsDeleted = true;
        graph.nodes[i] = node; // write back so storage-backed lists persist the flag
      }
    }
  }

  public IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top,
    IFilterExpression contentFilter = null)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection) || queryVector is null || queryVector.Length == 0 || top <= 0)
      return [];

    var graph = GetGraphForCollection(collection);
    if (graph.entrypoint is null || graph.entrypoint.Vector.Length == 0) // empty graph (placeholder only)
      return [];

    var destination = CreateQueryNode(queryVector);
    var searchTop = Math.Max(top, Options.SearchPruning);
    var neighbours = this.KNearest(collection, destination, searchTop);

    var resultsByKey = new Dictionary<string, (VectorEntry entry, float score)>(StringComparer.Ordinal);

    foreach (var node in neighbours)
    {
      var nodeKey = Convert.ToBase64String(node.Id.Value);
      var score = 1f - destination.TravelingCosts.From(node, usecache: false);

      if (!resultsByKey.TryGetValue(nodeKey, out var existing) || score > existing.score)
      {
        var content = store.GetContent(collection, node.Id.Value);
        if (content is null) continue;
        if (contentFilter != null && !MatchesFilter(content, contentFilter)) continue;

        resultsByKey[nodeKey] = (new VectorEntry { Id = node.Id.Value, CollectionName = collection, Content = content }, score);
      }
    }

    return resultsByKey.Values
      .OrderByDescending(r => r.score)
      .Take(top);
  }

  public IEnumerable<VectorEntry> Search(IStore store, string collection, IFilterExpression contentFilter = null)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection))
      yield break;

    if (!store.GetCollectionIndexOf(collection, out var index))
      yield break;

    var results = new List<VectorEntry>();

    foreach (var key in index.Keys)
    {
      var content = store.GetContent(collection, key);
      if (content is null)
        continue;

      if (contentFilter != null && !MatchesFilter(content, contentFilter))
        continue;

      yield return new VectorEntry()
      {
        Id = key,
        CollectionName = collection,
        Content = content
      };
    }
  }

  private static bool MatchesFilter(ReadOnlyMemory<byte> serializedContent, IFilterExpression filter)
  {
    if (filter == null) return true;

    try
    {
      var json = MessagePackSerializer.ConvertToJson(serializedContent,
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance));
      using var doc = JsonDocument.Parse(json);
      return filter.Matches(doc.RootElement);
    }
    catch
    {
      return false;
    }
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
    if (entrypoint is null || entrypoint.Vector.Length == 0) return []; // empty graph (placeholder only)

    var bestPeer = entrypoint;
    for (int level = entrypoint.MaxLevel; level > 0; --level)
    {
      bestPeer = this.KNearestAtLevel(collection, bestPeer, destination, 1, level).Single();
    }

    return this.KNearestAtLevel(collection, bestPeer, destination, k, 0);
  }

  internal bool IsDeleted(string collection, int positionId)
  {
    var coll = GetGraphForCollection(collection);
    return coll.nodes[positionId].IsDeleted;
  }

  public Task FlushAsync()
  {
    foreach (var graph in _collectionGraphs.Values)
      (graph.nodes as StoredList<IndexNode, SmallWorldOptions>)?.Flush();

    return Task.CompletedTask;
  }

  public Task ShrinkAsync()
  {
    foreach (var graph in _collectionGraphs.Values)
      (graph.nodes as StoredList<IndexNode, SmallWorldOptions>)?.ShrinkDb();

    return Task.CompletedTask;
  }

  private static float DefaultDistance(IndexNode left, IndexNode right)
  {
    if (left.Vector is null || right.Vector is null || left.Vector.Length == 0 || right.Vector.Length == 0)
      return float.MaxValue;

    return CosineDistance.SIMDForUnits(left.Vector, right.Vector);
  }

  private static string SanitizeCollectionName(string collection)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var buffer = collection.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
    return new string(buffer);
  }

  private IndexNode CreateQueryNode(float[] queryVector)
  {
    var vector = GC.AllocateUninitializedArray<float>(queryVector.Length);
    queryVector.CopyTo(vector, 0);
    NormalizeInPlace(vector);

    return new IndexNode(Options)
    {
      Id = new VectorKey { Value = Array.Empty<byte>() },
      MaxLevel = 0,
      Connections = Array.Empty<IList<int>>(),
      Vector = vector
    };
  }

  private static void NormalizeInPlace(Span<float> vector)
  {
    if (vector.Length == 0) return;

    var norm = TensorPrimitives.Norm(vector);
    if (norm <= 0) return;

    TensorPrimitives.Divide(vector, norm, vector);
  }
}