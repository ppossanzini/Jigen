using Hikyaku;
using Jigen.Core.Dto.collections;

namespace Jigen.Core.Query.collections;

public class SearchCollections : IRequest<SearchCollectionsResult>
{
  public string Database { get; set; }
  public SearchCollectionsData Data { get; set; }
}
