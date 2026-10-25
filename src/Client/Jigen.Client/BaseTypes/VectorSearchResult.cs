namespace Jigen.Client.BaseTypes;

public class VectorSearchResult<T>
  where T : class, new()
{
  public VectorKey Key { get; set; }
  public T Content { get; set; }

  /// <summary>Fused score for a hybrid search, the similarity otherwise.</summary>
  public float Score { get; set; }

  /// <summary>Dense similarity, when the document came from the dense half.</summary>
  public float? DenseScore { get; set; }

  /// <summary>Sparse score, when the document came from the sparse half.</summary>
  public float? SparseScore { get; set; }
}
