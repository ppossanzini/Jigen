using Hikyaku;
using Jigen.DataStructures;

namespace Jigen.Core.Query.collections;

public class GetAllKeys : IRequest<IEnumerable<VectorKey>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
}