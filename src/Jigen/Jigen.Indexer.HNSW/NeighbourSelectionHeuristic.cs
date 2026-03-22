namespace Jigen.Indexer;

/// <summary>
/// Type of heuristic to select best neighbours for a node.
/// </summary>
public enum NeighbourSelectionHeuristic
{
  /// <summary>
  /// Marker for the Algorithm 3 (SELECT-NEIGHBORS-SIMPLE) from the article.
  /// Implemented in <see cref="SmallWorld{TItem, TDistance}.NodeAlg3"/>
  /// </summary>
  SelectSimple,

  /// <summary>
  /// Marker for the Algorithm 4 (SELECT-NEIGHBORS-HEURISTIC) from the article.
  /// Implemented in <see cref="SmallWorld{TItem, TDistance}.NodeAlg4"/>
  /// </summary>
  SelectHeuristic,
}