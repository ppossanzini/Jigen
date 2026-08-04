namespace Jigen.API.Dto;

public class BulkDocumentItem
{
  public string Key { get; set; }
  public string KeyType { get; set; }
  public object Payload { get; set; }
  public string Sentence { get; set; }
}
