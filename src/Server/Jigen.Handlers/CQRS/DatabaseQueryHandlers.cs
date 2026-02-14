using Hikyaku;
using Jigen.Core.Dto.database;
using Jigen.Core.Query.database;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class DatabaseQueryHandlers(
  DatabasesManager manager,
  SystemDB master) :
  IRequestHandler<Core.Query.database.GetInfo, Core.Dto.database.DatabaseInfo>,
  IRequestHandler<Core.Query.database.ListDatabases, IEnumerable<string>>

{
  public Task<DatabaseInfo> Handle(GetInfo request, CancellationToken cancellationToken)
  {
    var dbInfo = master.System[SystemDB.BASEINFO];
    if (!dbInfo.Databases.Contains(request.Database)) throw new ArgumentException("Database not found");

    var store = manager.ActiveDatabases[request.Database];

    var filenames = store.GetFileNames().ToArray();

    var collections = store.GetCollections().Select(i => store.GetCollectionInfo(i)).ToArray();

    return Task.FromResult(new DatabaseInfo()
    {
      Name = request.Database,
      AllocatedContentSize = filenames.Where(i => i.EndsWith($"{StoreOptions.ContentSuffix}.jigen")).Select(i => new FileInfo(i).Length).FirstOrDefault(),
      AllocatedVectorSize = filenames.Where(i => i.EndsWith($"{StoreOptions.EmbeddingSuffix}.jigen")).Select(i => new FileInfo(i).Length).FirstOrDefault(),
      Collections = collections,
      Vectors = collections.Sum(i => i.Vectors)
    });
  }

  public Task<IEnumerable<string>> Handle(ListDatabases request, CancellationToken cancellationToken)
  {
    var dbInfo = master.System[SystemDB.BASEINFO];
    return Task.FromResult(dbInfo.Databases.AsEnumerable());
  }
}