namespace Jigen.DataStructures;

/// <summary>
/// A typed vector-search hit: the same shape the gRPC client returns, for the
/// in-process collections.
/// </summary>
public class VectorSearchResult<T>
  where T : class, new()
{
  public VectorKey Key { get; set; }
  public T Content { get; set; }

  /// <summary>
  /// Final score: the dense similarity for a dense search, the sparse score for
  /// a sparse one, and the fused rank value for a hybrid search.
  /// </summary>
  public float Score { get; set; }

  /// <summary>Dense similarity, when the document came from the dense half.</summary>
  public float? DenseScore { get; set; }

  /// <summary>Sparse score, when the document came from the sparse half.</summary>
  public float? SparseScore { get; set; }
}
