using Hikyaku;
using Jigen.Core.Command.database;
using Jigen.Core.Dto.database;
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
  Hikyaku.IRequestHandler<Core.Command.database.DeleteDatabase>,
  Hikyaku.IRequestHandler<Core.Command.database.SetDatabaseUsers>
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
    var info = NormalizeInfo(master.System[SystemDB.BASEINFO]);
    info.Databases.Add(request.Name);
    info.DatabaseInfos.Add(new DatabaseSystemInfo
    {
      Database = request.Name,
      CreatedAtUtc = DateTime.UtcNow,
      Users = []
    });

    master.System[SystemDB.BASEINFO] = info;
    await master.SaveChangesAsync();
    // master.RefreshReading();

    logger.LogInformation("Database " + request.Name + " created");
  }

  public async Task Handle(DeleteDatabase request, CancellationToken cancellationToken)
  {
    logger.LogInformation("Creating database: " + request.Name);

    var info = NormalizeInfo(master.System[SystemDB.BASEINFO]);
    if (info.Databases.Contains(request.Name))
      info.Databases.Remove(request.Name);

    var databaseInfo = info.DatabaseInfos.FirstOrDefault(i =>
      string.Equals(i.Database, request.Name, StringComparison.OrdinalIgnoreCase));

    if (databaseInfo != null)
      info.DatabaseInfos.Remove(databaseInfo);

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

  public async Task Handle(SetDatabaseUsers request, CancellationToken cancellationToken)
  {
    var info = NormalizeInfo(master.System[SystemDB.BASEINFO]);
    if (!info.Databases.Contains(request.Database))
      throw new ArgumentException("Database not found");

    var databaseInfo = info.DatabaseInfos.FirstOrDefault(i =>
      string.Equals(i.Database, request.Database, StringComparison.OrdinalIgnoreCase));

    if (databaseInfo == null)
    {
      databaseInfo = new DatabaseSystemInfo
      {
        Database = request.Database,
        Users = []
      };
      info.DatabaseInfos.Add(databaseInfo);
    }

    var incomingUsers = request.Data?.Users ?? [];
    databaseInfo.Users = incomingUsers
      .Where(u => !string.IsNullOrWhiteSpace(u.UserId) || !string.IsNullOrWhiteSpace(u.UserName))
      .Select(u => new DatabaseUserAssociation
      {
        UserId = u.UserId,
        UserName = u.UserName
      })
      .DistinctBy(u => string.IsNullOrWhiteSpace(u.UserId) ? $"name:{u.UserName}" : $"id:{u.UserId}")
      .ToList();

    master.System[SystemDB.BASEINFO] = info;
    await master.SaveChangesAsync();
  }

  private static SystemInfo NormalizeInfo(SystemInfo info)
  {
    info.Databases ??= [];
    info.DatabaseInfos ??= [];

    foreach (var db in info.Databases)
    {
      var exists = info.DatabaseInfos.Any(i => string.Equals(i.Database, db, StringComparison.OrdinalIgnoreCase));
      if (!exists)
      {
        info.DatabaseInfos.Add(new DatabaseSystemInfo
        {
          Database = db,
          Users = []
        });
      }
    }

    foreach (var databaseInfo in info.DatabaseInfos)
      databaseInfo.Users ??= [];

    return info;
  }
}