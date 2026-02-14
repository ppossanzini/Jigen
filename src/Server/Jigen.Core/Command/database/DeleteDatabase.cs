using Hikyaku;

namespace Jigen.Core.Command.database;

public class DeleteDatabase : IRequest
{
  public string Name { get; set; }

  public bool DeleteDatabaseFiles { get; set; }
}