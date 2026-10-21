namespace Jigen.API.Dto;

/// <summary>
/// Fusion of a dense and a lexical ranking. The weights must be declared
/// whenever both vectors are supplied: a zero weight leaves that modality out
/// and both at zero is rejected. With a single vector the missing weight
/// defaults to one.
/// </summary>
public class HybridSearchData
{
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
  public SparsePayload Sparse { get; set; }
  public int Top { get; set; } = 10;
  public float? DenseWeight { get; set; }
  public float? SparseWeight { get; set; }

  /// <summary>Return keys and scores only, without the content.</summary>
  public bool NoContent { get; set; }
}
