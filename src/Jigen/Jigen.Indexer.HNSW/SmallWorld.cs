using Jigen.DataStructures;
using Jigen.Indexer.Extensions;
using Jigen.Persistance;

namespace Jigen.Indexer;

internal delegate IList<IndexNode> SelectForConnectingDelegate(IndexNode item, IList<IndexNode> candidates,  SmallWorld smallworld);

public class SmallWorld : IIndexer
{
  private IndexNode _entrypoint;

  internal SmallWorldOptions Options { get; init; }
  internal IStore Store { get; init; }
  internal StoredList<IndexNode> Nodes { get ; private set; }

  internal readonly SelectForConnectingDelegate SelectBestForConnecting = null;


  public SmallWorld(SmallWorldOptions options, IStore store)
  {
    this.Options = options;
    this.Store = store;
    this.SelectBestForConnecting = options.NeighbourHeuristic switch
    {
      NeighbourSelectionHeuristic.SelectHeuristic => NodeExtensions.SelectBestForConnectingAlg4,
      NeighbourSelectionHeuristic.SelectSimple => NodeExtensions.SelectBestForConnectingAlg3,
      _ => NodeExtensions.SelectBestForConnectingAlg3
    };
  }

  public void OpenOrCreateIndex(string collection)
  {
    Nodes = new StoredList<IndexNode>(new StoreListOptions()
    {
      FilePath = collection,
      FlushInterval = TimeSpan.FromMinutes(1)
    });

    // Trick node in position 0 is the entrypoint, 
    // It can change so.. i ignore the node in zero position 
    // but reat che "PositionId" to obtain thre real entrypoint
    if (Nodes.Count > 0)
    {
      var entrypointId = Nodes[0].PositionId;
      _entrypoint = Nodes[entrypointId];
    }
  }

  public void AddToIndex(VectorEntry entry)
  {
    var newNode = entry.ToNode(Options);
    Nodes.AddNewNode(newNode);

    var bestPeer = _entrypoint;
    for (var level = bestPeer.MaxLevel; level > newNode.MaxLevel; --level)
    {
      bestPeer = this.KNearestAtLevel(bestPeer, newNode, 1, level).Single();
    }

    for (var level = Math.Min(newNode.MaxLevel, _entrypoint.MaxLevel); level >= 0; --level)
    {
      var potentialNeighbours = this.KNearestAtLevel(bestPeer, newNode, Options.ConstructionPruning, level);
      var bestNeighbours = SelectBestForConnecting(newNode, potentialNeighbours, this);

      foreach (var newNeighbour in bestNeighbours)
      {
        newNode.AddConnection(newNeighbour, level, this);
        newNeighbour.AddConnection(newNode, level, this);

        // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
        if (Tools.DLt(newNode.TravelingCosts.From(newNeighbour), newNode.TravelingCosts.From(bestPeer)))
        {
          bestPeer = newNeighbour;
        }
      }
    }

    // zoom out to the highest level
    if (newNode.MaxLevel <= _entrypoint.MaxLevel) return;

    _entrypoint = newNode;
    Nodes[0] = _entrypoint;
  }

  public void UpdateIndex(VectorEntry entry)
  {
    throw new NotImplementedException();
  }

  public void RemoveFromIndex(VectorEntry entry)
  {
    throw new NotImplementedException();
  }

  public List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top)
  {
    throw new NotImplementedException();
  }

  /// <summary>
  /// Run knn search for a given item.
  /// </summary>
  /// <param name="item">The item to search nearest neighbours.</param>
  /// <param name="k">The number of nearest neighbours.</param>
  /// <returns>The list of found nearest neighbours.</returns>
  public IList<KNNSearchResult> KNNSearch(TItem item, int k)
  {
    var destination = this._graph.CreateNewNode(-1, item, 0);
    var neighbourhood = this._graph.KNearest(destination, k);
    return neighbourhood.Select(n => new KNNSearchResult
    {
      Id = n.Id,
      Item = n.Item,
      Distance = destination.TravelingCosts.From(n),
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
  public IList<IndexNode> KNearest(IndexNode destination, int k)
  {
    var bestPeer = _entrypoint;
    for (int level = _entrypoint.MaxLevel; level > 0; --level)
    {
      bestPeer = this.KNearestAtLevel(bestPeer, destination, 1, level).Single();
    }

    return this.KNearestAtLevel(bestPeer, destination, k, 0);
  }
}