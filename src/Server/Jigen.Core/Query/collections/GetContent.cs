using Hikyaku;

namespace Jigen.Core.Query.collections;

public class GetContent: IRequest<object>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public Type ResultType { get; set; }
}