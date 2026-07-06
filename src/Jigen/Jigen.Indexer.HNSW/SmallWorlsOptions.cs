using Jigen.Indexer.Extensions;

namespace Jigen.Indexer;

public class SmallWorldOptions( int M = 10)
{
  public SmallWorldOptions(int m, int efConstruction, int efSearch, string storagePath) : this()
  {
    M = m;
    EfConstruction = efConstruction;
    EfSearch = efSearch;
    StoragePath = storagePath;
  }

  public Random generator { get; set; } = new Random();

  public Func<IndexNode, IndexNode, float> DefaultDistanceFunction { get; internal set; }

  public string StoragePath { get; set; } = Path.Combine(Path.GetTempPath(), "jigen-hnsw");
  public bool InMemory { get; set; }

  /// <summary>
  /// Gets or sets the parameter which defines the maximum number of neighbors in the zero and above-zero layers.
  /// The maximum number of neighbors for the zero layer is 2 * M.
  /// The maximum number of neighbors for higher layers is M.
  /// </summary>
  public int M { get; set; } = M;

  /// <summary>
  /// Gets or sets the max level decay parameter.
  /// https://en.wikipedia.org/wiki/Exponential_distribution
  /// See 'mL' parameter in the HNSW article.
  /// </summary>
  public double LevelLambda { get; set; } = 1 / Math.Log(M);

  /// <summary>
  /// Gets or sets parameter which specifies the type of heuristic to use for best neighbours selection.
  /// SelectHeuristic (Algorithm 4): same recall as SelectSimple but ~2x faster search
  /// on the resulting graph, at ~+30% construction cost.
  /// </summary>
  public NeighbourSelectionHeuristic NeighbourHeuristic { get; set; } = NeighbourSelectionHeuristic.SelectHeuristic;

  /// <summary>
  /// Gets or sets the number of candidates to consider as neighbousr for a given node at the graph construction phase.
  /// See 'efConstruction' parameter in the article.
  /// </summary>
  public int ConstructionPruning { get; set; } = 200;

  public int EfConstruction
  {
    get => ConstructionPruning;
    set => ConstructionPruning = value;
  }

  public int SearchPruning { get; set; } = 64;

  public int EfSearch
  {
    get => SearchPruning;
    set => SearchPruning = value;
  }

  /// <summary>
  /// Gets or sets a value indicating whether to expand candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used.
  /// See 'extendCandidates' parameter in the article.
  /// </summary>
  public bool ExpandBestSelection { get; set; } = false;

  /// <summary>
  /// Gets or sets a value indicating whether to keep pruned candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used.
  /// See 'keepPrunedConnections' parameter in the article.
  /// </summary>
  public bool KeepPrunedConnections { get; set; } = true;

  /// <summary>
  /// Slots of the in-memory node cache of a disk-backed graph (rounded up to
  /// a power of two). Every graph hop reads a node: a miss costs two file
  /// reads plus deserialization, so size this close to the expected node
  /// count of hot collections. A fully warm cache costs roughly
  /// slots × (vector + adjacency lists) of memory.
  /// </summary>
  public int NodeCacheSize { get; set; } = 65536;
}