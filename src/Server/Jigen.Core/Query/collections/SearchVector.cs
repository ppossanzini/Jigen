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

  /// <summary>Drop results scoring below this similarity.</summary>
  public float? MinScore { get; set; }
}

public class SearchVectorResultItem
{
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }

  /// <summary>
  /// The ranking score: the modality score on a single-modality search, the
  /// fused score on a hybrid one.
  /// </summary>
  public float Score { get; set; }

  /// <summary>True when the dense search produced the entry.</summary>
  public bool InDense { get; set; }

  /// <summary>True when the lexical search produced the entry.</summary>
  public bool InSparse { get; set; }

  /// <summary>Original dense similarity; 0 when the entry is absent from the dense ranking.</summary>
  public float DenseScore { get; set; }

  /// <summary>Original lexical score; 0 when the entry is absent from the sparse ranking.</summary>
  public float SparseScore { get; set; }
}
