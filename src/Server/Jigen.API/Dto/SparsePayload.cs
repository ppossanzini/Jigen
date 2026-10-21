namespace Jigen.API.Dto;

/// <summary>
/// Sparse lexical vector: term ids and their weights, one weight per term in
/// the same order. Terms are caller-defined ids, unique, strictly increasing
/// and non-negative; weights are finite and non-negative with at least one
/// above zero. Omit the object to store no lexical slot.
/// </summary>
public class SparsePayload
{
  public int[] Terms { get; set; }
  public float[] Weights { get; set; }
}
