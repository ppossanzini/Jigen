using Hikyaku;
using Jigen.Core.Dto.database;

namespace Jigen.Core.Command.database;

public class SetDatabaseUsers : IRequest
{
  public string Database { get; set; }
  public SetDatabaseUsersData Data { get; set; }
}
