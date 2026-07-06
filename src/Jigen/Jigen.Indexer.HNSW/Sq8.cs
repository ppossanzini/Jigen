using System.Numerics;
using System.Runtime.CompilerServices;

namespace Jigen.Indexer;

/// <summary>
/// Scalar 8-bit quantization kernels. Vectors are unit-normalized before
/// quantization, so components fit [-1,1] with a fixed scale of 127.
/// </summary>
internal static class Sq8
{
  public const float Scale = 127f;
  public const float InverseSquaredScale = 1f / (Scale * Scale);

  public static sbyte[] Quantize(ReadOnlySpan<float> vector)
  {
    var quantized = new sbyte[vector.Length];
    for (var i = 0; i < vector.Length; i++)
      quantized[i] = (sbyte)MathF.Round(Math.Clamp(vector[i], -1f, 1f) * Scale);
    return quantized;
  }

  /// <summary>
  /// SIMD int8 dot product with int32 accumulation. A single product is at
  /// most 127² = 16129, which fits a short: widen once, multiply as shorts,
  /// widen the products to int and accumulate.
  /// </summary>
  public static int Dot(ReadOnlySpan<sbyte> a, ReadOnlySpan<sbyte> b)
  {
    var sum = 0;
    var i = 0;

    if (Vector.IsHardwareAccelerated && a.Length >= Vector<sbyte>.Count)
    {
      var accumulator = Vector<int>.Zero;
      var lastBlock = a.Length - Vector<sbyte>.Count;

      for (; i <= lastBlock; i += Vector<sbyte>.Count)
      {
        var va = new Vector<sbyte>(a.Slice(i));
        var vb = new Vector<sbyte>(b.Slice(i));

        Vector.Widen(va, out Vector<short> aLow, out var aHigh);
        Vector.Widen(vb, out Vector<short> bLow, out var bHigh);

        var productLow = aLow * bLow;
        var productHigh = aHigh * bHigh;

        Vector.Widen(productLow, out Vector<int> p0, out var p1);
        Vector.Widen(productHigh, out Vector<int> p2, out var p3);

        accumulator += p0 + p1 + p2 + p3;
      }

      sum = Vector.Sum(accumulator);
    }

    for (; i < a.Length; i++)
      sum += a[i] * b[i];

    return sum;
  }

  /// <summary>Dot between a float vector and a quantized one (mixed graphs
  /// only: float records next to SQ8 records). Scalar: cold compatibility path.</summary>
  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public static float MixedDot(ReadOnlySpan<float> floats, ReadOnlySpan<sbyte> quantized)
  {
    var sum = 0f;
    for (var i = 0; i < floats.Length; i++)
      sum += floats[i] * quantized[i];
    return sum / Scale;
  }
}
