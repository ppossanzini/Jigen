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

public partial class SmallWorldIndexer : IIndexer, IExplorableIndex
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

  internal void AddToIndex(VectorEntry entry)
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
        // Per-node locks: adjacency mutation, pruning (which uses the node's
        // TravelingCosts cache) and the write-through persist must be atomic
        // per node, but two inserts touching DIFFERENT nodes proceed in parallel.
        lock (newNode)
        {
          newNode.AddConnection(newNeighbour, level, this, collection, graph);
        }

        lock (newNeighbour)
        {
          newNeighbour.AddConnection(newNode, level, this, collection, graph);
          graph.nodes[newNeighbour.PositionId] = newNeighbour;
        }

        // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
        if (Tools.DLt(newNode.TravelingCosts.From(newNeighbour), newNode.TravelingCosts.From(bestPeer)))
          bestPeer = newNeighbour;
      }
    }

    // Persist the newly constructed adjacency lists for the inserted node.
    lock (newNode)
    {
      graph.nodes[newNode.PositionId] = newNode;
    }

    // Only the owner thread fills newNode's distance cache (prunes use local
    // TravelingCosts instances), so this release needs no lock — but it must
    // happen: graph nodes live forever and the cache would leak per insert.
    newNode.TravelingCosts.ClearCache();

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
    if (store is null || string.IsNullOrWhiteSpace(collection) || queryVector is null || queryVector.Length == 0 || top <= 0)
      return [];

    var graph = GetGraphForCollection(collection);
    if (graph.entrypoint is null || graph.entrypoint.VectorDimensions == 0) // empty graph (placeholder only)
      return [];

    var destination = CreateQueryNode(queryVector);
    var searchTop = Math.Max(top, Options.SearchPruning);
    var neighbours = this.KNearest(collection, destination, searchTop);

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
        var content = store.GetContent(collection, node.Id.Value);
        if (content is null) continue;
        if (contentFilter != null && !MatchesFilter(content, contentFilter)) continue;

        if (exactRerank)
        {
          var embedding = store.GetEmbedding(collection, node.Id.Value);
          if (embedding is { Length: > 0 })
            score = TensorPrimitives.CosineSimilarity(queryVector, embedding);
        }

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
    if (entrypoint is null || entrypoint.VectorDimensions == 0) return []; // empty graph (placeholder only)

    var bestPeer = entrypoint;
    for (int level = entrypoint.MaxLevel; level > 0; --level)
    {
      // A level can hold no live node (heavy deletions): keep descending
      // from the current peer.
      var nearest = this.KNearestAtLevel(collection, bestPeer, destination, 1, level);
      if (nearest.Count > 0) bestPeer = nearest[0];
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

  private static float DefaultDistance(IndexNode left, IndexNode right)
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
    var invalid = Path.GetInvalidFileNameChars();
    var buffer = collection.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
    return new string(buffer);
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