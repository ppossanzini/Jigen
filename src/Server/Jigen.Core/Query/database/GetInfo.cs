using Hikyaku;

namespace Jigen.Core.Query.database;

public class GetInfo: IRequest<Core.Dto.database.DatabaseInfo>
{
  public string Database { get; set; }
}