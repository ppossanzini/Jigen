using System.Numerics.Tensors;

namespace Jigen.Extensions;

public static class VectorsExtensions
{
  public static Span<float> Normalize(this float[] vector)
  {
    Span<float> destination = new float[vector.Length];
    var distance = TensorPrimitives.Norm(vector);
    TensorPrimitives.Divide(vector, distance, destination);
    return destination;
  }
  
  public static Span<float> Normalize(this ReadOnlySpan<float> vector)
  {
    Span<float> destination = new float[vector.Length];
    var distance = TensorPrimitives.Norm(vector);
    TensorPrimitives.Divide(vector, distance, destination);
    return destination;
  }

  public static void Normalize(this ReadOnlySpan<float> vector, Span<float> destination)
  {
    var distance = TensorPrimitives.Norm(vector);
    TensorPrimitives.Divide(vector, distance, destination);
  }

  public static Span<sbyte> Quantize(this Span<float> vector)
  {
    Span<sbyte> quantized = new sbyte[vector.Length];
    for (int i = 0; i < vector.Length; i++)
      quantized[i] = (sbyte)MathF.Round(Math.Clamp(vector[i], -1f, 1f) * 127.0f);

    return quantized;
  }

  public static Span<float> DeQuantize(this Span<sbyte> vector)
  {
    Span<float> dequantized = new float[vector.Length];
    for (int i = 0; i < vector.Length; i++)
      dequantized[i] = vector[i] / (127.0f * 127.0f);

    return dequantized;
  }
}