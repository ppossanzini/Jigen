namespace Jigen.Core.Dto.collections;

using System.Text.Json;

public class SearchCollectionsResult
{
  public double EmbeddingsCalculationTime { get; set; }
  public double SearchTime { get; set; }
  public double MergeTime { get; set; }
  public double SortingTime { get; set; }
  public IEnumerable<CollectionSearchResult> CollectionsResults { get; set; }
  public IEnumerable<CollectionSearchResultItem> MergedResults { get; set; }
}

public class CollectionSearchResult
{
  public string Collection { get; set; }
  public double SearchTime { get; set; }
  public IEnumerable<CollectionSearchResultItem> Results { get; set; }
}

public class CollectionSearchResultItem
{
  public byte[] Key { get; set; }
  public object Content { get; set; }

  /// <summary>Fused rank score for a hybrid search, the dense similarity otherwise.</summary>
  public float Score { get; set; }

  /// <summary>Dense similarity, when the document came from the dense half.</summary>
  public float? DenseScore { get; set; }

  /// <summary>Sparse score, when the document came from the sparse half.</summary>
  public float? SparseScore { get; set; }
}
