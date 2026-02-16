using Hikyaku;

namespace Jigen.Core.Command.collections;

public class Contains: IRequest<bool>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}