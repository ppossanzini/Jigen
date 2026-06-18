using Hikyaku;
using Jigen.Core.Dto.database;

namespace Jigen.Core.Query.database;

public class ListDatabaseUsers : IRequest<IEnumerable<DatabaseUserInfo>>
{
  public string Database { get; set; }
}
