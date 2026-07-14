using Hikyaku;
using Jigen.DataStructures;

namespace Jigen.Core.Query.collections;

public class GetCollectionGraph : IRequest<IndexGraphSnapshot>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public int Dimensions { get; set; } = 2;
  public int Limit { get; set; } = 2000;
  public int? Level { get; set; }
}
