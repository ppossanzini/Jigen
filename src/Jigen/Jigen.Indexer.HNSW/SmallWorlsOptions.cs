using Jigen.Indexer.Extensions;

namespace Jigen.Indexer;

public class SmallWorldOptions( int M = 10)
{
  public Random generator { get; set; } = new Random();

  public Func<IndexNode, IndexNode, float> DefaultDistanceFunction { get; internal set; }

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
  /// </summary>
  public NeighbourSelectionHeuristic NeighbourHeuristic { get; set; } = NeighbourSelectionHeuristic.SelectSimple;

  /// <summary>
  /// Gets or sets the number of candidates to consider as neighbousr for a given node at the graph construction phase.
  /// See 'efConstruction' parameter in the article.
  /// </summary>
  public int ConstructionPruning { get; set; } = 200;

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
}