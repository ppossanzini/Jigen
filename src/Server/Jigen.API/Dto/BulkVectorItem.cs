namespace Jigen.API.Dto;

public class BulkVectorItem
{
  public string Key { get; set; }
  public string KeyType { get; set; }
  public object Payload { get; set; }
  public float[] Embeddings { get; set; }
}
