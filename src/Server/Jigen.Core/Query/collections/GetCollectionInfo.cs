using Hikyaku;

namespace Jigen.Core.Query.collections;

public class GetCollectionInfo : IRequest<Core.Dto.database.CollectionInfo>
{
  public string Database { get; set; }
  public string Collection { get; set; }
}