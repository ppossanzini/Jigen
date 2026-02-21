using Microsoft.Extensions.Options;

namespace Jigen.Handlers.Model;

public class DatabasesManager
{
  private readonly SystemDB _master;
  private readonly JigenServerSettings _settings;
    
  public DatabasesManager(SystemDB master, IOptions<JigenServerSettings> settings)
  {
    this._master = master;
    this._settings = settings.Value;
    Init();
  }
  
  public Dictionary<string, Store> ActiveDatabases { get; init; } = new();

  public void Init()
  {
    var dbs = _master.System[SystemDB.BASEINFO].Databases;
    foreach (var db in dbs)
    {
      if(ActiveDatabases.ContainsKey(db))
        continue;
      ActiveDatabases.Add(db, new Store(new StoreOptions(){DataBaseName = db, DataBasePath = _settings.DataFolderPath}));
    }
  }

  public void FlushAndStopALL()
  {
  }
}