using Hikyaku;

namespace Jigen.Core.Command.collections;

public class Count: IRequest<int>
{
  public string Database { get; set; }
  public string Collection { get; set; }
}