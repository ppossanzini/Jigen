namespace Jigen.Indexer;

/// <summary>
/// Pure-C# PCA via power iteration with deflation. Deterministic (fixed RNG seed).
/// </summary>
internal static class PcaProjection
{
  private const int MaxIterations = 100;
  private const double ConvergenceEpsilon = 1e-7;

  /// <summary>Returns [n][k] coordinates, each column scaled to [-1, 1]. Rows whose
  /// length differs from the first non-null row get all-zero coordinates.</summary>
  public static float[][] Project(IReadOnlyList<float[]> vectors, int components)
  {
    var n = vectors.Count;
    var result = new float[n][];
    for (var i = 0; i < n; i++) result[i] = new float[components];
    if (n < 2) return result;

    var d = 0;
    foreach (var v in vectors)
      if (v is not null && v.Length > 0) { d = v.Length; break; }
    if (d == 0) return result;
    var k = Math.Min(components, Math.Min(d, n));

    // 1. Copy + center (double accumulation for the mean).
    var rows = new List<(int index, float[] row)>(n);
    for (var i = 0; i < n; i++)
    {
      var v = vectors[i];
      if (v is null || v.Length != d) continue;   // mismatched rows keep zero coords
      rows.Add((i, (float[])v.Clone()));
    }
    if (rows.Count < 2) return result;

    var mean = new double[d];
    foreach (var (_, row) in rows)
      for (var j = 0; j < d; j++) mean[j] += row[j];
    for (var j = 0; j < d; j++) mean[j] /= rows.Count;
    foreach (var (_, row) in rows)
      for (var j = 0; j < d; j++) row[j] = (float)(row[j] - mean[j]);

    var rng = new Random(12345);

    for (var c = 0; c < k; c++)
    {
      // 2. Random unit start vector.
      var v = new double[d];
      double norm = 0;
      for (var j = 0; j < d; j++) { v[j] = rng.NextDouble() - 0.5; norm += v[j] * v[j]; }
      norm = Math.Sqrt(norm);
      if (norm <= 0) break;
      for (var j = 0; j < d; j++) v[j] /= norm;

      // 3. Power iteration on C = X^T X (never materialize C): w = X^T (X v).
      for (var iter = 0; iter < MaxIterations; iter++)
      {
        var w = new double[d];
        foreach (var (_, row) in rows)
        {
          double s = 0;
          for (var j = 0; j < d; j++) s += row[j] * v[j];
          for (var j = 0; j < d; j++) w[j] += s * row[j];
        }
        double wnorm = 0;
        for (var j = 0; j < d; j++) wnorm += w[j] * w[j];
        wnorm = Math.Sqrt(wnorm);
        if (wnorm < 1e-12) break;   // no variance left
        double dot = 0;
        for (var j = 0; j < d; j++) { w[j] /= wnorm; dot += w[j] * v[j]; }
        v = w;
        if (Math.Abs(dot) > 1 - ConvergenceEpsilon) break;
      }

      // 4. Scores become coordinate c; deflate rows.
      foreach (var (index, row) in rows)
      {
        double t = 0;
        for (var j = 0; j < d; j++) t += row[j] * v[j];
        result[index][c] = (float)t;
        for (var j = 0; j < d; j++) row[j] = (float)(row[j] - t * v[j]);
      }
    }

    // 5. Normalize each column to [-1, 1].
    for (var c = 0; c < components; c++)
    {
      float max = 0;
      for (var i = 0; i < n; i++) max = Math.Max(max, Math.Abs(result[i][c]));
      if (max > 0)
        for (var i = 0; i < n; i++) result[i][c] /= max;
    }

    return result;
  }
}
