using Hikyaku;

namespace Jigen.Core.Command.database;

public class CreateDatabase : IRequest
{
  public string Name { get; set; }
}