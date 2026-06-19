using Hikyaku;
using Jigen.Core.Dto.database;
using Jigen.Core.Query.database;
using Jigen.Handlers.Model;

namespace Jigen.Handlers.CQRS;

public class DatabaseQueryHandlers(
  DatabasesManager manager,
  DatabaseOwnershipGuard ownershipGuard) :
  IRequestHandler<Core.Query.database.GetInfo, Core.Dto.database.DatabaseInfo>,
  IRequestHandler<Core.Query.database.ListDatabases, IEnumerable<string>>,
  IRequestHandler<Core.Query.database.GetDetails, Core.Dto.database.DatabaseDetails>,
  IRequestHandler<Core.Query.database.ListDatabaseUsers, IEnumerable<Core.Dto.database.DatabaseUserInfo>>

{
  public Task<DatabaseInfo> Handle(GetInfo request, CancellationToken cancellationToken)
  {
    ownershipGuard.EnsureCanReadDatabase(request.Database);

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
    var dbInfo = ownershipGuard.GetNormalizedSystemInfo();
    return Task.FromResult(ownershipGuard.GetReadableDatabases(dbInfo));
  }

  public Task<DatabaseDetails> Handle(GetDetails request, CancellationToken cancellationToken)
  {
    ownershipGuard.EnsureCanReadDatabase(request.Database);

    var dbInfo = ownershipGuard.GetNormalizedSystemInfo();

    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store))
      throw new ArgumentException("Database not found");

    var filenames = store.GetFileNames().ToArray();
    var collections = store.GetCollections().Select(i => store.GetCollectionInfo(i)).ToArray();
    var databaseInfo = dbInfo.DatabaseInfos.FirstOrDefault(i =>
      string.Equals(i.Database, request.Database, StringComparison.OrdinalIgnoreCase));

    return Task.FromResult(new DatabaseDetails
    {
      Name = request.Database,
      CreatedAtUtc = databaseInfo?.CreatedAtUtc,
      AllocatedContentSize = filenames.Where(i => i.EndsWith($"{StoreOptions.ContentSuffix}.jigen")).Select(i => new FileInfo(i).Length).FirstOrDefault(),
      AllocatedVectorSize = filenames.Where(i => i.EndsWith($"{StoreOptions.EmbeddingSuffix}.jigen")).Select(i => new FileInfo(i).Length).FirstOrDefault(),
      Collections = collections,
      Vectors = collections.Sum(i => i.Vectors),
      Users = (databaseInfo?.Users ?? [])
        .Select(u => new DatabaseUserInfo
        {
          UserId = u.UserId,
          UserName = u.UserName
        })
        .ToArray()
    });
  }

  public Task<IEnumerable<DatabaseUserInfo>> Handle(ListDatabaseUsers request, CancellationToken cancellationToken)
  {
    ownershipGuard.EnsureCanReadDatabase(request.Database);

    var dbInfo = ownershipGuard.GetNormalizedSystemInfo();

    var databaseInfo = dbInfo.DatabaseInfos.FirstOrDefault(i =>
      string.Equals(i.Database, request.Database, StringComparison.OrdinalIgnoreCase));

    var users = (databaseInfo?.Users ?? [])
      .Select(u => new DatabaseUserInfo
      {
        UserId = u.UserId,
        UserName = u.UserName
      })
      .ToArray();

    return Task.FromResult(users.AsEnumerable());
  }
}