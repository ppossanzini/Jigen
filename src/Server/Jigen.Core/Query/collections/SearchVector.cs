using Hikyaku;
using Jigen.Filtering;

namespace Jigen.Core.Query.collections;

public class SearchVector : IRequest<IEnumerable<SearchVectorResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; }
  public IFilterExpression Filter { get; set; }

  /// <summary>Per-query HNSW beam width; 0 = index default. Ignored by exact indexes.</summary>
  public int EfSearch { get; set; }

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }

  /// <summary>
  /// Sparse half of the query: term identifier to weight. When present the
  /// search becomes hybrid and the two halves are fused by reciprocal rank.
  /// </summary>
  public IReadOnlyDictionary<int, float> SparseQuery { get; set; }

  /// <summary>
  /// Drop dense hits below this similarity, on a dense-only search as well as on
  /// the dense half of a hybrid one.
  /// </summary>
  public float? MinDenseScore { get; set; }

  /// <summary>Drop sparse hits below this score before the fusion.</summary>
  public float? MinSparseScore { get; set; }
}

public class SearchVectorResultItem
{
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }

  /// <summary>Fused score for a hybrid search, the similarity otherwise.</summary>
  public float Score { get; set; }

  /// <summary>Dense similarity, when the document came from the dense half.</summary>
  public float? DenseScore { get; set; }

  /// <summary>Sparse score, when the document came from the sparse half.</summary>
  public float? SparseScore { get; set; }
}
