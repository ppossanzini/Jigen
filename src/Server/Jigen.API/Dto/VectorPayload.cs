namespace Jigen.API.Dto;

public class VectorPayload
{
  public object Payload { get; set; }
  public float[] Embeddings { get; set; }

  /// <summary>Every vector of a multi-vector entry; overrides <see cref="Embeddings"/> when set.</summary>
  public float[][] Vectors { get; set; }
}
