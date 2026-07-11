using Hikyaku;
using Jigen.Core.Query.collections;
using Jigen.DataStructures;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;

namespace Jigen.Handlers.CQRS;

public class GraphQueryHandlers(
  DatabasesManager manager,
  ILogger<GraphQueryHandlers> logger) :
  IRequestHandler<GetCollectionGraph, IndexGraphSnapshot>
{
  public Task<IndexGraphSnapshot> Handle(GetCollectionGraph request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetCollectionGraph for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store))
      throw new ArgumentException("Database not found");

    if (store.Options.Indexer is not IExplorableIndex explorable)
      return Task.FromResult(new IndexGraphSnapshot
      {
        Collection = request.Collection,
        Dimensions = Math.Clamp(request.Dimensions, 2, 3)
      });

    return Task.FromResult(explorable.GetGraphSnapshot(
      request.Collection, request.Dimensions, request.Limit, request.Level));
  }
}
