using Hikyaku;
using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Exact lexical search over the sparse slot: entries sharing at least one
/// term with the query, ranked by the sum of the shared weight products.
/// </summary>
public class SearchSparse : IRequest<IEnumerable<SearchVectorResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public SparseVector Sparse { get; set; }
  public int Top { get; set; }
  public IFilterExpression Filter { get; set; }

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }

  /// <summary>Drop results scoring below this value.</summary>
  public float? MinScore { get; set; }
}
