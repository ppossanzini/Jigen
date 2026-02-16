using Hikyaku;

namespace Jigen.Core.Query.collections;

public class GetRawContent : IRequest<byte[]> 
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}