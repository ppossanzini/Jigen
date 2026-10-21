namespace Jigen.DataStructures;

/// <summary>
/// One result of fusing two ranked lists.
/// </summary>
public readonly record struct FusedRank(byte[] Key, double Score, bool InDense, bool InSparse);

/// <summary>
/// Reciprocal rank fusion: orders two ranked lists by position instead of by
/// score. Similarity scores from a dense search and weights from a lexical
/// search live on different scales, so summing them directly buries one of the
/// two; positions are comparable by construction.
/// </summary>
public static class RankFusion
{
  /// <summary>Constant of the reciprocal rank formula, as used by the products that adopt it.</summary>
  public const int DefaultK = 60;

  /// <summary>
  /// Fuses two ranked lists into a single ordering. A weight of zero excludes
  /// that list without changing the other's contribution; both weights at zero
  /// is rejected because there would be nothing to order. Ties are broken by
  /// ascending key, so the same input always yields the same output.
  /// </summary>
  public static IReadOnlyList<FusedRank> Fuse(
    IReadOnlyList<byte[]> denseOrder,
    IReadOnlyList<byte[]> sparseOrder,
    float denseWeight = 1f,
    float sparseWeight = 1f,
    int top = int.MaxValue,
    int k = DefaultK)
  {
    if (denseWeight < 0 || sparseWeight < 0)
      throw new ArgumentException("Fusion weights are non-negative.");
    if (float.IsNaN(denseWeight) || float.IsNaN(sparseWeight)
        || float.IsInfinity(denseWeight) || float.IsInfinity(sparseWeight))
      throw new ArgumentException("Fusion weights must be finite numbers.");
    if (denseWeight == 0 && sparseWeight == 0)
      throw new ArgumentException("At least one fusion weight must be greater than zero.");
    if (k <= 0)
      throw new ArgumentOutOfRangeException(nameof(k), "The fusion constant must be greater than zero.");

    if (top <= 0) return [];

    var scores = new Dictionary<byte[], (double Score, bool Dense, bool Sparse)>(KeyComparer.Instance);

    Accumulate(denseOrder, denseWeight);
    Accumulate(sparseOrder, sparseWeight);

    return scores
      .OrderByDescending(pair => pair.Value.Score)
      .ThenBy(pair => pair.Key, KeyOrderComparer.Instance)
      .Take(top)
      .Select(pair => new FusedRank(pair.Key, pair.Value.Score, pair.Value.Dense, pair.Value.Sparse))
      .ToArray();

    void Accumulate(IReadOnlyList<byte[]> order, float weight)
    {
      if (order is null || weight == 0) return;

      for (var position = 0; position < order.Count; position++)
      {
        var key = order[position];
        if (key is null) continue;

        var contribution = weight / (double)(k + position + 1);
        if (scores.TryGetValue(key, out var current))
          scores[key] = (current.Score + contribution,
            current.Dense || ReferenceEquals(order, denseOrder),
            current.Sparse || ReferenceEquals(order, sparseOrder));
        else
          scores[key] = (contribution,
            ReferenceEquals(order, denseOrder),
            ReferenceEquals(order, sparseOrder));
      }
    }
  }

  private sealed class KeyComparer : IEqualityComparer<byte[]>
  {
    public static readonly KeyComparer Instance = new();

    public bool Equals(byte[] x, byte[] y)
    {
      if (ReferenceEquals(x, y)) return true;
      if (x is null || y is null) return false;
      return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode(byte[] key) => key is null ? 0 : (int)System.IO.Hashing.XxHash32.HashToUInt32(key);
  }

  private sealed class KeyOrderComparer : IComparer<byte[]>
  {
    public static readonly KeyOrderComparer Instance = new();

    public int Compare(byte[] x, byte[] y)
    {
      if (ReferenceEquals(x, y)) return 0;
      if (x is null) return -1;
      if (y is null) return 1;
      return x.AsSpan().SequenceCompareTo(y);
    }
  }
}
