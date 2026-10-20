using Hikyaku;
using Jigen.Filtering;

namespace Jigen.Core.Query.metadata;

/// <summary>
/// Grouped search: one result per shared metadata record, ranked by the
/// highest observed embedding score. CandidateBudget is mandatory and caps
/// the sum of candidate counts requested across attempts, repeated candidates
/// included. Fewer than Top groups may be returned. Exact grouped top-k is
/// not guaranteed by approximate indexes.
/// </summary>
public class SearchByMetadata : IRequest<IEnumerable<SearchByMetadataResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public float[] Embeddings { get; set; }
  public string Sentence { get; set; }
  public int Top { get; set; }
  public int CandidateBudget { get; set; }
  public IFilterExpression Filter { get; set; }

  /// <summary>Per-query index beam width; 0 = index default.</summary>
  public int EfSearch { get; set; }

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }

  /// <summary>Drop groups scoring below this similarity.</summary>
  public float? MinScore { get; set; }
}

public class SearchByMetadataResultItem
{
  public int MetadataIndex { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float Score { get; set; }
}
