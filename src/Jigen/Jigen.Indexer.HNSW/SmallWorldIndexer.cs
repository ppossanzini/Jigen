using System.Collections.Concurrent;
using System.Text.Json;
using Jigen.DataStructures;
using Jigen.Filtering;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;
using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Indexer;

internal delegate IList<IndexNode> SelectForConnectingDelegate(IndexNode item, IList<IndexNode> candidates, int level, SmallWorldIndexer smallworld, string collection);

public partial class SmallWorldIndexer : IIndexer, IExplorableIndex, IBatchIndexer
{
  internal SmallWorldOptions Options { get; init; }

  private readonly ConcurrentDictionary<string, (IndexNode entrypoint, IList<IndexNode> nodes)> _collectionGraphs = new();
  private readonly Lock _graphCreationLock = new();

  // Pool of epoch-stamped visited sets for SEARCH-LAYER traversals: renting
  // one is O(1) (an epoch bump) versus clearing nodeCount bytes per level.
  private readonly ConcurrentBag<VisitedSet> _visitedPool = new();

  internal VisitedSet RentVisitedSet(int minSize)
  {
    if (!_visitedPool.TryTake(out var set)) set = new VisitedSet();
    set.Prepare(minSize);
    return set;
  }

  internal void ReturnVisitedSet(VisitedSet set) => _visitedPool.Add(set);

  // Pool of BinaryHeap<IndexNode> instances: KNearestAtLevel allocates two
  // heaps per call (result window + expansion candidates). Recycling them
  // spares Gen0 the per-search churn; the T[] buffers grow once and stay.
  private readonly ConcurrentBag<BinaryHeap<IndexNode>> _heapPool = new();

  internal BinaryHeap<IndexNode> RentHeap(IComparer<IndexNode> comparer, int minCapacity = 16)
  {
    if (!_heapPool.TryTake(out var heap)) heap = new BinaryHeap<IndexNode>();
    heap.Initialize(comparer, minCapacity);
    return heap;
  }

  /// <summary>
  /// Hot-path overload: rents a heap driven by a <see cref="Comparison{T}"/>
  /// delegate, bypassing <see cref="IComparer{T}"/> and its virtual dispatch.
  /// </summary>
  internal BinaryHeap<IndexNode> RentHeap(Comparison<IndexNode> comparison, int minCapacity = 16)
  {
    if (!_heapPool.TryTake(out var heap)) heap = new BinaryHeap<IndexNode>();
    heap.Initialize(comparison, minCapacity);
    return heap;
  }

  internal void ReturnHeap(BinaryHeap<IndexNode> heap)
  {
    heap.Clear();
    _heapPool.Add(heap);
  }

  /// <summary>
  /// Rents a heap and heapifies <paramref name="source"/> into it, driven
  /// by a <see cref="Comparison{T}"/> delegate (construction pruning path).
  /// </summary>
  internal BinaryHeap<IndexNode> RentHeap(IList<IndexNode> source, Comparison<IndexNode> comparison)
  {
    if (!_heapPool.TryTake(out var heap)) heap = new BinaryHeap<IndexNode>();
    heap.Initialize(source, comparison);
    return heap;
  }

  // Key → node positions, built lazily on the first delete: without it every
  // RemoveFromIndex scans (and, for disk graphs, deserializes) the whole node
  // list. Duplicate keys are possible (an overwrite inserts a new node), hence
  // the list. Accessed only under lock(graph.nodes), so plain Dictionary.
  private readonly ConcurrentDictionary<string, Dictionary<VectorKey, List<int>>> _keyIndexes = new();

  // Requires lock(graph.nodes).
  private Dictionary<VectorKey, List<int>> GetKeyIndex(string collection, (IndexNode entrypoint, IList<IndexNode> nodes) graph)
  {
    if (_keyIndexes.TryGetValue(collection, out var map)) return map;

    map = new Dictionary<VectorKey, List<int>>(graph.nodes.Count);
    for (var i = 1; i < graph.nodes.Count; i++)
    {
      var node = graph.nodes[i];
      if (node.IsDeleted || node.Id.Value is null || node.Id.Value.Length == 0) continue;

      if (!map.TryGetValue(node.Id, out var positions))
        map[node.Id] = positions = new List<int>(1);
      positions.Add(i);
    }

    _keyIndexes[collection] = map;
    return map;
  }

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
      MigrateLegacyGraphPaths(collection, filePath);

      IList<IndexNode> nodes;
      if (Options.InMemory)
        nodes  = new List<IndexNode>();
      else
        nodes = OpenDiskGraph(filePath);

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

  /// <summary>
  /// Opens (or migrates) the split disk storage of a collection graph:
  /// immutable vectors in {name}.hnsw.vec, in-place adjacency records in
  /// {name}.hnsw.adj. A single-file legacy graph at {name}.hnsw is converted
  /// once and then removed; an interrupted migration restarts from scratch
  /// (the legacy file is only deleted after a successful flush).
  /// </summary>
  private SplitNodeList OpenDiskGraph(string legacyPath)
  {
    var flushInterval = TimeSpan.FromMinutes(1);
    var nodes = new SplitNodeList($"{legacyPath}.vec", $"{legacyPath}.adj", Options, flushInterval);

    if (!File.Exists(legacyPath)) return nodes;

    var legacy = new StoredList<IndexNode, SmallWorldOptions>(
      new StoreListOptions { FilePath = legacyPath, FlushInterval = flushInterval }, Options);
    try
    {
      if (legacy.Count > 0)
      {
        if (nodes.Count != 0) nodes.Clear(); // interrupted previous migration

        var entryPointer = 0;
        for (var i = 0; i < legacy.Count; i++)
        {
          IndexNode node;
          try
          {
            node = legacy[i];
          }
          catch (Exception)
          {
            // Corrupt legacy record: the slot must survive (indexes are
            // adjacency targets) but as a deleted placeholder; the store
            // reconciliation can re-add the entry from its embedding.
            node = new IndexNode(Options)
            {
              PositionId = i, IsDeleted = true, Id = new VectorKey { Value = [] },
              Vector = [], MaxLevel = 0, Connections = Array.Empty<IList<int>>()
            };
          }

          if (i == 0)
          {
            // Legacy slot 0 is a full copy of the entrypoint; the new format
            // stores a placeholder plus the pointer, written below.
            entryPointer = node.PositionId;
            nodes.Add(new IndexNode(Options)
            {
              PositionId = 0, Id = new VectorKey { Value = [] },
              Vector = [], MaxLevel = 0, Connections = Array.Empty<IList<int>>()
            });
            continue;
          }

          node.PositionId = i;
          nodes.Add(node);
        }

        if (entryPointer > 0 && entryPointer < nodes.Count)
          nodes[0] = new IndexNode(Options) { PositionId = entryPointer, Id = new VectorKey { Value = [] } };

        nodes.Flush();
      }
    }
    finally
    {
      legacy.DisposeAsync().GetAwaiter().GetResult();
    }

    File.Delete(legacyPath);
    if (File.Exists($"{legacyPath}.index")) File.Delete($"{legacyPath}.index");

    return nodes;
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

  internal void AddToIndex(VectorEntry entry) => AddToIndex(entry, null);

  public void AddBatchToIndex(IReadOnlyList<VectorEntry> entries)
  {
    var dirtyByGraph = new Dictionary<IList<IndexNode>, HashSet<IndexNode>>();
    Exception lastError = null;
    try
    {
      foreach (var entry in entries)
      {
        try { AddToIndex(entry, dirtyByGraph); }
        catch (Exception ex) { lastError = ex; }
      }
    }
    finally
    {
      PersistDirtyNodes(dirtyByGraph);
    }

    if (lastError is not null)
      throw new InvalidOperationException("One or more HNSW batch entries failed to index.", lastError);
  }

  private static void MarkDirty(
    Dictionary<IList<IndexNode>, HashSet<IndexNode>> dirtyByGraph,
    IList<IndexNode> nodes, IndexNode node)
  {
    if (!dirtyByGraph.TryGetValue(nodes, out var dirty))
      dirtyByGraph[nodes] = dirty = new HashSet<IndexNode>();
    dirty.Add(node);
  }

  private static void PersistDirtyNodes(
    Dictionary<IList<IndexNode>, HashSet<IndexNode>> dirtyByGraph)
  {
    foreach (var (nodes, dirtyNodes) in dirtyByGraph)
      foreach (var node in dirtyNodes)
        lock (node)
          nodes[node.PositionId] = node;
  }

  private void AddToIndex(VectorEntry entry,
    Dictionary<IList<IndexNode>, HashSet<IndexNode>> batchDirty)
  {
    if (entry is null || entry.Id is null || string.IsNullOrWhiteSpace(entry.CollectionName) || entry.Embedding.IsEmpty)
      return;

    var collection = entry.CollectionName;
    var graph = GetGraphForCollection(collection);
    var newNode = entry.ToNode(Options);

    // hnswlib-style concurrency: the graph lock covers only node allocation
    // and entrypoint changes; adjacency wiring takes per-node locks, so
    // inserts into the same collection run in parallel.
    // Lock order everywhere: graph.nodes → node → storage. Never node → graph.
    IndexNode entrypoint;
    lock (graph.nodes)
    {
      graph = GetGraphForCollection(collection); // refresh entrypoint under lock

      if (graph.entrypoint is { VectorDimensions: > 0 } &&
          graph.entrypoint.VectorDimensions != newNode.VectorDimensions)
        throw new ArgumentException(
          $"Collection '{collection}' uses {graph.entrypoint.VectorDimensions} dimensions; received {newNode.VectorDimensions}.",
          nameof(entry));

      graph.nodes.AddNewNode(newNode);

      // Keep the delete lookup aligned if it was already built (the map is
      // only ever touched under the graph lock).
      if (_keyIndexes.TryGetValue(collection, out var keyIndex))
      {
        if (!keyIndex.TryGetValue(newNode.Id, out var positions))
          keyIndex[newNode.Id] = positions = new List<int>(1);
        positions.Add(newNode.PositionId);
      }

      // The initial slot-0 placeholder has an empty vector (distance = MaxValue):
      // promote the first real node to entrypoint so the placeholder never
      // becomes part of the graph.
      if (graph.entrypoint is null || graph.entrypoint.VectorDimensions == 0)
      {
        AssignEntryPoint(collection, graph, newNode);
        return;
      }

      entrypoint = graph.entrypoint;
    }

    // ---- concurrent wiring phase (no graph lock held) ----------------------

    var bestPeer = entrypoint;
    var dirtyByGraph = batchDirty ?? new Dictionary<IList<IndexNode>, HashSet<IndexNode>>();
    var ownsDirtySet = batchDirty is null;
    try
    {
      for (var level = bestPeer.MaxLevel; level > newNode.MaxLevel; --level)
      {
        // A level can hold no live node (heavy deletions): keep descending
        // from the current peer instead of failing the insert.
        var nearest = this.KNearestAtLevel(collection, bestPeer, newNode, 1, level);
        if (nearest.Count > 0) bestPeer = nearest[0];
      }

      for (var level = Math.Min(newNode.MaxLevel, entrypoint.MaxLevel); level >= 0; --level)
      {
        var potentialNeighbours = this.KNearestAtLevel(collection, bestPeer, newNode, Options.ConstructionPruning, level);
        var bestNeighbours = SelectBestForConnecting(newNode, potentialNeighbours, level, this, collection);

        foreach (var newNeighbour in bestNeighbours)
        {
          // Mutations remain immediately visible through immutable adjacency
          // snapshots. Persistence is coalesced below: the same neighbour can
          // be touched at several levels but is written only once per insert.
          lock (newNode)
          {
            newNode.AddConnection(newNeighbour, level, this, collection, graph);
          }

          lock (newNeighbour)
          {
            newNeighbour.AddConnection(newNode, level, this, collection, graph);
            MarkDirty(dirtyByGraph, graph.nodes, newNeighbour);
          }

          // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
          if (Tools.DLt(newNode.TravelingCosts.From(newNeighbour), newNode.TravelingCosts.From(bestPeer)))
            bestPeer = newNeighbour;
        }
      }
    }
    finally
    {
      // Serialize each dirty canonical node under its node lock so concurrent
      // inserts/deletes cannot publish a newer snapshot between serialization
      // and write-through. The finally preserves the previous durability
      // behaviour even when graph wiring fails part-way through.
      MarkDirty(dirtyByGraph, graph.nodes, newNode);
      if (ownsDirtySet)
        PersistDirtyNodes(dirtyByGraph);

      // Only the owner thread fills newNode's distance cache (prunes use local
      // TravelingCosts instances), so release it when the operation completes.
      newNode.TravelingCosts.ClearCache();
    }

    // zoom out to the highest level; a deleted entrypoint (legacy graph, or
    // every node deleted) is also replaced, so searches restart from a live node
    if (newNode.MaxLevel > entrypoint.MaxLevel || entrypoint.IsDeleted)
    {
      lock (graph.nodes)
      {
        graph = GetGraphForCollection(collection); // the entrypoint may have moved meanwhile
        if (newNode.MaxLevel > graph.entrypoint.MaxLevel || graph.entrypoint.IsDeleted)
          AssignEntryPoint(collection, graph, newNode);
      }
    }
  }

  public void RemoveFromIndex(string collection, byte[] key)
  {
    if (string.IsNullOrWhiteSpace(collection) || key is null) return;

    var graph = GetGraphForCollection(collection);
    lock (graph.nodes)
    {
      graph = GetGraphForCollection(collection); // refresh entrypoint under lock

      // O(1) lookup instead of scanning (and deserializing) every node; the
      // list covers duplicate keys left by overwrites.
      var keyIndex = GetKeyIndex(collection, graph);
      if (!keyIndex.Remove(new VectorKey { Value = key }, out var positions)) return;

      var entrypointDeleted = false;

      foreach (var i in positions)
      {
        var node = graph.nodes[i];
        if (node.IsDeleted) continue;

        // Node lock: a concurrent insert may be persisting this node's
        // adjacency right now (lock order graph → node, like everywhere).
        lock (node)
        {
          node.IsDeleted = true;
          graph.nodes[i] = node; // write back so storage-backed lists persist the flag
        }

        if (graph.entrypoint is not null && node.PositionId == graph.entrypoint.PositionId)
          entrypointDeleted = true;
      }

      if (entrypointDeleted)
      {
        // The cached entrypoint may be a different instance than the one just
        // written back (storage-backed lists deserialize fresh objects): flag
        // it too, so searches never return it while a replacement is picked.
        graph.entrypoint.IsDeleted = true;
        ReassignEntryPoint(collection, graph);
      }
    }
  }

  // Requires the graph lock. Promotes the highest-level live node to
  // entrypoint; with no live node left the deleted entrypoint stays as a
  // navigation-only anchor (searches filter deleted nodes from results).
  private void ReassignEntryPoint(string collection, (IndexNode entrypoint, IList<IndexNode> nodes) graph)
  {
    IndexNode best = null;
    for (var i = 1; i < graph.nodes.Count; i++)
    {
      var node = graph.nodes[i];
      if (node.IsDeleted || node.VectorDimensions == 0) continue;
      if (best is null || node.MaxLevel > best.MaxLevel) best = node;
    }

    if (best is not null)
      AssignEntryPoint(collection, graph, best);
  }

  public IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top,
    IFilterExpression contentFilter = null)
  {
    return Search(store, collection, queryVector, top, efSearch: 0, contentFilter);
  }

  public IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top,
    int efSearch, IFilterExpression contentFilter = null)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection) || queryVector is null || queryVector.Length == 0 || top <= 0)
      return [];

    var graph = GetGraphForCollection(collection);
    if (graph.entrypoint is null || graph.entrypoint.VectorDimensions == 0) // empty graph (placeholder only)
      return [];
    if (queryVector.Length != graph.entrypoint.VectorDimensions)
      throw new ArgumentException(
        $"Collection '{collection}' uses {graph.entrypoint.VectorDimensions} dimensions; received {queryVector.Length}.",
        nameof(queryVector));

    var destination = CreateQueryNode(queryVector);
    var searchTop = Math.Max(top, efSearch > 0 ? efSearch : Options.SearchPruning);

    // ACORN-1 style filtered search: the metadata filter is evaluated during
    // graph traversal (see KNearestAtLevel) so the ef-sized result window
    // fills with candidates that already satisfy it, instead of running an
    // unfiltered search and post-filtering a fixed window — which starves
    // `top` under a selective filter even when plenty of matches exist
    // elsewhere in the graph. Content read during the filter check is cached
    // so the loop below doesn't hit the store a second time for the same node.
    Dictionary<VectorKey, byte[]> contentCache = null;
    Func<IndexNode, bool> accept = null;
    if (contentFilter != null)
    {
      contentCache = new Dictionary<VectorKey, byte[]>();
      accept = node =>
      {
        var content = store.GetContent(collection, node.Id.Value);
        contentCache[node.Id] = content;
        return content is not null && MatchesFilter(content, contentFilter);
      };
    }

    var neighbours = this.KNearest(collection, destination, searchTop, accept);

    // VectorKey compares and hashes the raw bytes: no Base64 allocation per result.
    var resultsByKey = new Dictionary<VectorKey, (VectorEntry entry, float score)>(neighbours.Count);

    // With SQ8 the graph scores are approximate: rescore the (few) candidates
    // with the store's full-precision embeddings before the final ranking.
    var exactRerank = Options.Quantization == VectorQuantization.SQ8 && Options.ExactRerank;

    foreach (var node in neighbours)
    {
      var nodeKey = node.Id;
      var score = 1f - destination.TravelingCosts.From(node, usecache: false);

      if (!resultsByKey.TryGetValue(nodeKey, out var existing) || score > existing.score)
      {
        var content = contentCache != null && contentCache.TryGetValue(nodeKey, out var cached)
          ? cached
          : store.GetContent(collection, node.Id.Value);
        if (content is null) continue;

        if (exactRerank)
        {
          var embedding = store.GetEmbedding(collection, node.Id.Value);
          if (embedding is { Length: > 0 })
            score = TensorPrimitives.CosineSimilarity(queryVector, embedding);
        }

        resultsByKey[nodeKey] = (new VectorEntry { Id = node.Id.Value, CollectionName = collection, Content = content }, score);
      }
    }

    // The candidate window is small (normally efSearch), but this is still a
    // query hot path: materialize and sort directly instead of building the
    // OrderBy/Take iterator pipeline and its auxiliary enumerable objects.
    var results = new List<(VectorEntry entry, float score)>(resultsByKey.Count);
    results.AddRange(resultsByKey.Values);
    results.Sort(static (left, right) => right.score.CompareTo(left.score));
    if (results.Count > top)
      results.RemoveRange(top, results.Count - top);
    return results;
  }

  public IEnumerable<VectorEntry> Search(IStore store, string collection, IFilterExpression contentFilter = null)
  {
    if (store is null || string.IsNullOrWhiteSpace(collection))
      yield break;

    if (!store.GetCollectionIndexOf(collection, out var index))
      yield break;

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
    return MessagePackFilterEvaluator.Matches(serializedContent, filter);
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
    var results = new List<KNNSearchResult>(neighbourhood.Count);
    for (var i = 0; i < neighbourhood.Count; i++)
    {
      var node = neighbourhood[i];
      results.Add(new KNNSearchResult
      {
        Id = node.PositionId,
        Item = node,
        Distance = item.TravelingCosts.From(node),
      });
    }
    return results;
  }

  /// <summary>
  /// Get k nearest items for a given one.
  /// Contains implementation of K-NN-SEARCH(hnsw, q, K, ef) algorithm.
  /// Article: Section 4. Algorithm 5.
  /// </summary>
  /// <param name="destination">The given node to get the nearest neighbourhood for.</param>
  /// <param name="k">The size of the neighbourhood.</param>
  /// <returns>The list of the nearest neighbours.</returns>
  public IList<IndexNode> KNearest(string collection, IndexNode destination, int k, Func<IndexNode, bool> accept = null)
  {
    var graph = GetGraphForCollection(collection);
    var entrypoint = graph.entrypoint;
    if (entrypoint is null || entrypoint.VectorDimensions == 0) return []; // empty graph (placeholder only)

    var bestPeer = entrypoint;
    for (int level = entrypoint.MaxLevel; level > 0; --level)
    {
      // A level can hold no live node (heavy deletions): keep descending
      // from the current peer. Upper-level descent is pure navigation
      // (picks the next greedy entry point), so it ignores the filter.
      var nearest = this.KNearestAtLevel(collection, bestPeer, destination, 1, level);
      if (nearest.Count > 0) bestPeer = nearest[0];
    }

    return this.KNearestAtLevel(collection, bestPeer, destination, k, 0, accept);
  }

  internal bool IsDeleted(string collection, int positionId)
  {
    var coll = GetGraphForCollection(collection);
    return coll.nodes[positionId].IsDeleted;
  }

  public Task FlushAsync()
  {
    foreach (var graph in _collectionGraphs.Values)
      (graph.nodes as SplitNodeList)?.Flush();

    return Task.CompletedTask;
  }

  /// <summary>
  /// Aligns every collection graph with the store: nodes whose key no longer
  /// exists in the store are marked deleted, and store entries missing from
  /// the graph (index updates lost in a crash: the graph flushes on its own
  /// cadence) are re-indexed from their persisted embeddings.
  /// </summary>
  public Task ReconcileAsync(IStore store)
  {
    if (store is null) return Task.CompletedTask;

    foreach (var collection in store.GetCollections())
    {
      if (!store.GetCollectionIndexOf(collection, out var index) || index is null) continue;

      var graph = GetGraphForCollection(collection);
      lock (graph.nodes)
      {
        graph = GetGraphForCollection(collection);

        // Reconciliation flips deletion flags and re-adds nodes in bulk: drop
        // the delete lookup and let it rebuild lazily on the next delete.
        _keyIndexes.TryRemove(collection, out _);

        // Graph → store: collect live keys, dropping nodes the store no longer knows.
        // Slot 0 aliases the entrypoint, which is re-visited at its own PositionId.
        var liveKeys = new HashSet<VectorKey>();
        var entrypointDeleted = false;
        for (var i = 1; i < graph.nodes.Count; i++)
        {
          var node = graph.nodes[i];
          if (node.IsDeleted || node.Id.Value is null || node.Id.Value.Length == 0) continue;

          if (!index.ContainsKey(node.Id.Value))
          {
            lock (node)
            {
              node.IsDeleted = true;
              graph.nodes[i] = node; // write back so storage-backed lists persist the flag
            }

            if (graph.entrypoint is not null && node.PositionId == graph.entrypoint.PositionId)
              entrypointDeleted = true;
            continue;
          }

          liveKeys.Add(node.Id);
        }

        if (entrypointDeleted)
        {
          graph.entrypoint.IsDeleted = true;
          ReassignEntryPoint(collection, graph);
          graph = GetGraphForCollection(collection); // pick up the new entrypoint
        }

        // Store → graph: re-index entries whose insert never reached the graph.
        foreach (var kv in index)
        {
          if (kv.Value.embeddingsposition <= 0) continue; // content-only entry
          if (liveKeys.Contains(new VectorKey { Value = kv.Key })) continue;

          var embedding = store.GetEmbedding(collection, kv.Key);
          if (embedding is null || embedding.Length == 0) continue;

          AddToIndex(new VectorEntry { Id = kv.Key, CollectionName = collection, Embedding = embedding });
        }
      }
    }

    return Task.CompletedTask;
  }

  public Task ShrinkAsync()
  {
    foreach (var graph in _collectionGraphs.Values)
      (graph.nodes as SplitNodeList)?.ShrinkDb();

    return Task.CompletedTask;
  }

  /// <summary>
  /// Flushes and releases every collection graph: without this the storage
  /// files stay open (with their flush loops running) after the store closes.
  /// The indexer stays usable: a later access reloads the graph from disk.
  /// </summary>
  public async Task CloseAsync()
  {
    foreach (var key in _collectionGraphs.Keys.ToList())
    {
      if (_collectionGraphs.TryRemove(key, out var graph) &&
          graph.nodes is SplitNodeList stored)
        await stored.DisposeAsync();
    }

    _keyIndexes.Clear();
  }

  /// <summary>Reference sentinel for the default distance function, so
  /// <see cref="TravelingCosts"/> can branch to the direct call when no
  /// custom distance has been configured (the common case).</summary>
  internal static readonly Func<IndexNode, IndexNode, float> DefaultDistanceFunc = DefaultDistance;

  internal static float DefaultDistance(IndexNode left, IndexNode right)
  {
    if (left.VectorDimensions == 0 || right.VectorDimensions == 0)
      return float.MaxValue;

    if (left.IsQuantized)
    {
      if (right.IsQuantized)
        return 1f - Sq8.Dot(left.QuantizedSpan, right.QuantizedSpan) * Sq8.InverseSquaredScale;

      // Mixed float/SQ8 records (graph quantized mid-life): compatibility path.
      return 1f - Sq8.MixedDot(right.VectorSpan, left.QuantizedSpan);
    }

    if (right.IsQuantized)
      return 1f - Sq8.MixedDot(left.VectorSpan, right.QuantizedSpan);

    // VectorSpan is zero-copy: RAM for fresh/query nodes, the memory-mapped
    // vector file for persisted ones — no deserialization on the hot path.
    return CosineDistance.SIMDForUnits(left.VectorSpan, right.VectorSpan);
  }

  private static string SanitizeCollectionName(string collection)
  {
    // Collection names are arbitrary user data. A replacement-based sanitizer
    // is not injective ("a/b" and "a_b" collide), so disk identity is the full
    // SHA-256 of the UTF-8 name. This is also independent of OS filename rules.
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(collection))).ToLowerInvariant();
  }

  private void MigrateLegacyGraphPaths(string collection, string newBasePath)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var legacyName = new string(collection.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    var legacyBasePath = Path.Combine(Options.StoragePath, $"{legacyName}.hnsw");
    if (string.Equals(legacyBasePath, newBasePath, StringComparison.Ordinal)) return;

    foreach (var suffix in new[] { "", ".index", ".vec", ".vec.index", ".adj", ".adj.index" })
    {
      var source = legacyBasePath + suffix;
      var destination = newBasePath + suffix;
      if (File.Exists(source) && !File.Exists(destination))
        File.Move(source, destination);
    }
  }

  private IndexNode CreateQueryNode(float[] queryVector)
  {
    var vector = GC.AllocateUninitializedArray<float>(queryVector.Length);
    queryVector.CopyTo(vector, 0);
    NormalizeInPlace(vector);

    var node = new IndexNode(Options)
    {
      Id = new VectorKey { Value = Array.Empty<byte>() },
      MaxLevel = 0,
      Connections = Array.Empty<IList<int>>(),
      Vector = vector
    };

    if (Options.Quantization == VectorQuantization.SQ8)
      node.RamQuantized = Sq8.Quantize(vector);

    return node;
  }

  private static void NormalizeInPlace(Span<float> vector)
  {
    if (vector.Length == 0) return;

    var norm = TensorPrimitives.Norm(vector);
    if (norm <= 0) return;

    TensorPrimitives.Divide(vector, norm, vector);
  }
}
