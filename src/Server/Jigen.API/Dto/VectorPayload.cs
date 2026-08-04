namespace Jigen.API.Dto;

public class VectorPayload
{
  public object Payload { get; set; }
  public float[] Embeddings { get; set; }
}
