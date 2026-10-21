using Hikyaku;
using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Runs the dense and the lexical search and fuses the two rankings by
/// position. Weights must be declared whenever both vectors are supplied: a
/// zero weight leaves that modality out, and both at zero is rejected.
/// </summary>
public class SearchHybrid : IRequest<IEnumerable<SearchVectorResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public float[] Embeddings { get; set; }
  public SparseVector Sparse { get; set; }
  public int Top { get; set; }
  public IFilterExpression Filter { get; set; }
  public float DenseWeight { get; set; } = 1f;
  public float SparseWeight { get; set; } = 1f;

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }
}
