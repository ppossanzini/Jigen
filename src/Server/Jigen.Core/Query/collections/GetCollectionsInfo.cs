using Hikyaku;
using Jigen.DataStructures;

namespace Jigen.Core.Query.collections;

public class GetCollectionsInfo : IRequest<IEnumerable<CollectionInfo>>
{
  public string Database { get; set; }
}