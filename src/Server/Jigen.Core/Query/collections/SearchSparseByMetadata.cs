using Hikyaku;
using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Grouped lexical search: one result per shared metadata record, ranked by
/// the highest lexical score observed. CandidateBudget is mandatory and caps
/// the entries requested from the term lists, so a group-rich collection
/// cannot turn one query into a full scan.
/// </summary>
public class SearchSparseByMetadata : IRequest<IEnumerable<SearchSparseByMetadataResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public SparseVector Sparse { get; set; }
  public int Top { get; set; }
  public int CandidateBudget { get; set; }
  public IFilterExpression Filter { get; set; }

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }
}

public class SearchSparseByMetadataResultItem
{
  public int MetadataIndex { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float Score { get; set; }
}
