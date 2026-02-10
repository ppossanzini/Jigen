using Hikyaku;

namespace Jigen.Core.Query.collections;

public class ListCollections : IRequest<IEnumerable<string>>
{
  public string Database { get; set; }
}