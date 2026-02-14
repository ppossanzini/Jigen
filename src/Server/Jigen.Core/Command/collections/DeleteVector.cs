using Hikyaku;

namespace Jigen.Core.Command.collections;

public class DeleteVector: IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}