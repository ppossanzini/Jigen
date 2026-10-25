namespace Jigen.Embedding.Core.Dto;

/// <summary>
/// Sparse lexical vector crossing the module boundary: tokenizer vocabulary ids
/// with their weights. Plain arrays, so it serialises over the mediator whether
/// the embedding module runs in-process or on a remote worker.
/// </summary>
public class SparseEmbeddingDto
{
  public int[] Terms { get; set; } = [];

  public float[] Weights { get; set; } = [];
}

/// <summary>
/// Dense and sparse vectors of the same text, produced from one input so the two
/// slots always describe the same sentence.
/// </summary>
public class VectorPairDto
{
  public float[] Dense { get; set; } = [];

  public SparseEmbeddingDto Sparse { get; set; } = new();
}
