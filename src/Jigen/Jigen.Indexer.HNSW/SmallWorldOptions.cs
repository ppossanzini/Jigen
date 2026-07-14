using Jigen.Indexer.Extensions;

namespace Jigen.Indexer;

public enum VectorQuantization
{
  None = 0,

  /// <summary>
  /// Scalar 8-bit quantization of the GRAPH vectors (components ∈ [-1,1]
  /// after normalization, scale 127): 4× smaller vector file and cheaper
  /// distances, at a small recall cost that <see cref="SmallWorldOptions.ExactRerank"/>
  /// recovers by rescoring results with the store's full-precision embeddings.
  /// Store embeddings are NOT touched. Applies to newly written records.
  /// </summary>
  SQ8 = 1
}

public class SmallWorldOptions( int M = 10)
{
  public SmallWorldOptions(int m, int efConstruction, int efSearch, string storagePath) : this()
  {
    M = m;
    EfConstruction = efConstruction;
    EfSearch = efSearch;
    StoragePath = storagePath;
  }

  // Random.Shared: thread-safe (thread-local state internally), so the
  // default insert path in GetMaxLevel needs no lock. A caller-supplied
  // Random (e.g. seeded for reproducible tests) is NOT thread-safe and is
  // still locked there — only the default benefits from the fast path.
  public Random generator { get; set; } = Random.Shared;

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

  /// <summary>Quantization of the graph-side vectors. See <see cref="VectorQuantization"/>.</summary>
  public VectorQuantization Quantization { get; set; } = VectorQuantization.None;

  /// <summary>
  /// With <see cref="VectorQuantization.SQ8"/>: rescore search results with
  /// the store's full-precision embeddings before ranking (default true).
  /// </summary>
  public bool ExactRerank { get; set; } = true;

  /// <summary>
  /// Currently unused — kept for API/config compatibility, not wired to any
  /// code path. <see cref="SplitNodeList"/> keeps every graph node (id,
  /// level, adjacency, delete flag) canonically resident in RAM by design,
  /// so indexing never deserializes on a graph hop; only the vectors are
  /// served from the memory-mapped file, and that mapping is not bounded by
  /// this setting either. See docs/indexes/hnsw.md.
  /// </summary>
  public int NodeCacheSize { get; set; } = 65536;

  /// <summary>
  /// Upper bound on nodes expanded during a level-0 search with a metadata
  /// filter, expressed as a multiple of the requested candidate count (ef).
  /// A filter is evaluated during traversal (see <see cref="SmallWorldIndexer.Search"/>):
  /// a selective filter can leave the result window short of ef for a long
  /// time, so this caps the search to a bounded partial scan instead of
  /// visiting the whole graph when the filter matches few or no nodes.
  /// Ignored for unfiltered searches and graph construction.
  /// </summary>
  public int FilteredSearchExpansionFactor { get; set; } = 20;
}