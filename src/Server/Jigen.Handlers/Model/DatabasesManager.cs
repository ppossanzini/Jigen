using Microsoft.Extensions.Options;

namespace Jigen.Handlers.Model;

public class DatabasesManager(IOptions<JigenServerSettings> settings)
{
  public Dictionary<string, Store> ActiveDatabases { get; init; } = new();
  
  public void FlushAndStopALL()
  {
    
  }
}