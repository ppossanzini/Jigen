using Hikyaku;

namespace Jigen.Core.Command.collections;

public class Clear: IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
}