using Hikyaku;
using Jigen.Core.Command.database;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class DatabaseCommandHandlers(
  DatabasesManager manager,
  SystemDB master,
  IOptions<JigenServerSettings> settings,
  ILogger<DatabaseCommandHandlers> logger
) :
  Hikyaku.IRequestHandler<Core.Command.database.CreateDatabase>,
  Hikyaku.IRequestHandler<Core.Command.database.DeleteDatabase>
{
  public async Task Handle(CreateDatabase request, CancellationToken cancellationToken)
  {
    logger.LogInformation("Creating database: " + request.Name);

    if (manager.ActiveDatabases.Keys.Contains(request.Name))
      throw new Exception("Database already exists");

    // Create the database and init its state
    manager.ActiveDatabases.Add(request.Name, new Store(new StoreOptions()
    {
      DataBasePath = settings.Value.DataFolderPath, DataBaseName = request.Name
    }));

    // Save DbInfo in master DB.
    var info = master.System[SystemDB.BASEINFO];
    info.Databases.Add(request.Name);

    master.System[SystemDB.BASEINFO] = info;
    await master.SaveChangesAsync();
    master.RefreshReading();

    logger.LogInformation("Database " + request.Name + " created");
  }

  public async Task Handle(DeleteDatabase request, CancellationToken cancellationToken)
  {
    logger.LogInformation("Creating database: " + request.Name);

    var info = master.System[SystemDB.BASEINFO];
    if (info.Databases.Contains(request.Name))
      info.Databases.Remove(request.Name);

    master.System[SystemDB.BASEINFO] = info;
    await master.SaveChangesAsync();

    // Close the database
    if (manager.ActiveDatabases.ContainsKey(request.Name))
    {
      await manager.ActiveDatabases[request.Name].Close();
      var filenames = manager.ActiveDatabases[request.Name].GetFileNames();
      manager.ActiveDatabases.Remove(request.Name);

      if (request.DeleteDatabaseFiles)
      {
        foreach (var dbfile in filenames)
        {
          if (File.Exists(dbfile))
            try
            {
              File.Delete(dbfile);
            }
            catch (Exception ex)
            {
              logger.LogError("Error deleting database file: " + dbfile + " - " + ex.Message);
              throw;
            }
        }
      }
    }

    logger.LogInformation("Database " + request.Name + " deleted");
  }
}