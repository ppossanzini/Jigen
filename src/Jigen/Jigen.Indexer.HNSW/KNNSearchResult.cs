// ReSharper disable InconsistentNaming

namespace Jigen.Indexer;

/// <summary>
/// Representation of knn search result.
/// </summary>
public class KNNSearchResult
{
  /// <summary>
  /// Gets or sets the id of the item = rank of the item in source collection.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Gets or sets the item itself.
  /// </summary>
  public IndexNode Item { get; set; }

  /// <summary>
  /// Gets or sets the distance between the item and the knn search query.
  /// </summary>
  public float Distance { get; set; }
}