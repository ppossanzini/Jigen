using System.Numerics.Tensors;

namespace Jigen.Extensions;

public static class VectorsExtensions
{
  public static void Normalize(this float[] vector, Span<float> destination)
  {
    var distance = TensorPrimitives.Norm(vector);
    TensorPrimitives.Divide(vector, distance, destination);
  }
  
  public static void Normalize(this ReadOnlySpan<float> vector, Span<float> destination)
  {
    var distance = TensorPrimitives.Norm(vector);
    TensorPrimitives.Divide(vector, distance, destination);
  }

  public static void Quantize(this ReadOnlySpan<float> vector, Span<sbyte> destination)
  {
    for (int i = 0; i < vector.Length; i++)
      destination[i] = (sbyte)MathF.Round(Math.Clamp(vector[i], -1f, 1f) * 127.0f);
  }

  public static void DeQuantize(this ReadOnlySpan<sbyte> vector, Span<float> destination)
  {
    // Inverse of Quantize: components were scaled by 127, not 127².
    for (int i = 0; i < vector.Length; i++)
      destination[i] = vector[i] / 127.0f;
  }
}