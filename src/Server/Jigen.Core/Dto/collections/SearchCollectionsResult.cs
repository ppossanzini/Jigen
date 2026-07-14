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
  public float Score { get; set; }
}
