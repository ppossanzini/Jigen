using Hikyaku;

namespace Jigen.Core.Query.collections;

public class GetCollectionsInfo : IRequest<IEnumerable<Core.Dto.database.CollectionInfo>>
{
  public string Database { get; set; }
}