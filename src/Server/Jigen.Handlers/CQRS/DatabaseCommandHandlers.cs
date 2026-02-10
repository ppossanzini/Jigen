using Hikyaku;
using Jigen.Core.Command.database;
using Jigen.Handlers.Model;
using MediatR;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class DatabaseCommandHandlers(IHikyaku mediator, DatabasesManager manager, SystemDB master, IOptions<JigenServerSettings> settings) :
  Hikyaku.IRequestHandler<Core.Command.database.CreateDatabase>,
  Hikyaku.IRequestHandler<Core.Command.database.DeleteDatabase>
{
  public Task Handle(CreateDatabase request, CancellationToken cancellationToken)
  {
    if (manager.ActiveDatabases.Keys.Contains(request.Name))
      throw new Exception("Database already exists");

    manager.ActiveDatabases.Add(request.Name, new Store(new StoreOptions()
    {
      DataBasePath = settings.Value.DataFolderPath, DataBaseName = request.Name
    }));

    var info =master.System[SystemDB.BASEINFO];
      
    info.Content.Databases = info.Content.Databases.ToList().Append(request.Name).ToArray();
    
  }

  public Task Handle(DeleteDatabase request, CancellationToken cancellationToken)
  {
  }
}