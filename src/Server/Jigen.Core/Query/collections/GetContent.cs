namespace Jigen.Core.Query.collections;

public class GetContent
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}