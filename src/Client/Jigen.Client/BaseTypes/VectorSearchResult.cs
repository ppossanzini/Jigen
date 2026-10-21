namespace Jigen.Client.BaseTypes;

public class VectorSearchResult<T>
  where T : class, new()
{
  public VectorKey Key { get; set; }
  public T Content { get; set; }

  /// <summary>
  /// The ranking score: the modality score on a single-modality search, the
  /// fused score on a hybrid one.
  /// </summary>
  public float Score { get; set; }

  /// <summary>True when the dense search produced the entry.</summary>
  public bool InDense { get; set; }

  /// <summary>True when the lexical search produced the entry.</summary>
  public bool InSparse { get; set; }

  /// <summary>Original dense similarity; 0 when absent from the dense ranking.</summary>
  public float DenseScore { get; set; }

  /// <summary>Original lexical score; 0 when absent from the lexical ranking.</summary>
  public float SparseScore { get; set; }
}
