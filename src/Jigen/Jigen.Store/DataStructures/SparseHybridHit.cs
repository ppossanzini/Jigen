using Jigen.DataStructures;

namespace Jigen;

/// <summary>
/// One hit of a hybrid search: the stored entry with the fused rank score and
/// the partial scores of the halves it came from.
/// </summary>
public sealed record SparseHybridHit(byte[] Key, VectorEntry Entry, double Score,
  float? DenseScore, float? SparseScore);
