namespace Jigen.API.Dto;

public class BulkVectorItem
{
  public string Key { get; set; }
  public string KeyType { get; set; }
  public object Payload { get; set; }

  /// <summary>Every vector of the entry, all with the collection dimensions. A single-vector entry is an array of one.</summary>
  public float[][] Vectors { get; set; }
}
