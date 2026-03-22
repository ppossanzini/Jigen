namespace Jigen.Indexer;

public class SmallWorldParameters
{
  /// <summary>
  /// Initializes a new instance of the <see cref="SmallWorldParameters"/> class.
  /// </summary>
  public SmallWorldParameters()
  {
    this.M = 10;
    this.LevelLambda = 1 / Math.Log(this.M);
    this.NeighbourHeuristic = NeighbourSelectionHeuristic.SelectSimple;
    this.ConstructionPruning = 200;
    this.ExpandBestSelection = false;
    this.KeepPrunedConnections = true;
  }

  /// <summary>
  /// Gets or sets the parameter which defines the maximum number of neighbors in the zero and above-zero layers.
  /// The maximum number of neighbors for the zero layer is 2 * M.
  /// The maximum number of neighbors for higher layers is M.
  /// </summary>
  public int M { get; set; }

  /// <summary>
  /// Gets or sets the max level decay parameter.
  /// https://en.wikipedia.org/wiki/Exponential_distribution
  /// See 'mL' parameter in the HNSW article.
  /// </summary>
  public double LevelLambda { get; set; }

  /// <summary>
  /// Gets or sets parameter which specifies the type of heuristic to use for best neighbours selection.
  /// </summary>
  public NeighbourSelectionHeuristic NeighbourHeuristic { get; set; }

  /// <summary>
  /// Gets or sets the number of candidates to consider as neighbousr for a given node at the graph construction phase.
  /// See 'efConstruction' parameter in the article.
  /// </summary>
  public int ConstructionPruning { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether to expand candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used.
  /// See 'extendCandidates' parameter in the article.
  /// </summary>
  public bool ExpandBestSelection { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether to keep pruned candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used.
  /// See 'keepPrunedConnections' parameter in the article.
  /// </summary>
  public bool KeepPrunedConnections { get; set; }
}