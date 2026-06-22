using Hikyaku;
using Jigen.Core.Dto.database;

namespace Jigen.Core.Query.database;

public class GetDetails : IRequest<DatabaseDetails>
{
  public string Database { get; set; }
}
