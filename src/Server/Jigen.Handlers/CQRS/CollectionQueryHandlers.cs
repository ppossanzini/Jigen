using Hikyaku;
using Jigen.Core.Query.collections;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class CollectionQueryHandlers(
  DatabasesManager manager,
  ILogger<CollectionCommandHandlers> logger) :
  IRequestHandler<Core.Query.collections.ListCollections, IEnumerable<string>>,
  IRequestHandler<Core.Query.collections.GetCollectionInfo, CollectionInfo>,
  IRequestHandler<Core.Query.collections.GetCollectionsInfo, IEnumerable<CollectionInfo>>,
  IRequestHandler<Core.Query.collections.GetRawContent, byte[]>,
  IRequestHandler<Core.Query.collections.GetAllKeys, IEnumerable<VectorKey>>,
  IRequestHandler<Core.Query.collections.SearchVector, IEnumerable<SearchVectorResultItem>>

{
  public Task<IEnumerable<string>> Handle(ListCollections request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing ListCollection for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(store.GetCollections().AsEnumerable());
  }

  public Task<CollectionInfo> Handle(GetCollectionInfo request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetCollectionInfo for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(store.GetCollectionInfo(request.Collection));
  }

  public Task<IEnumerable<CollectionInfo>> Handle(GetCollectionsInfo request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetCollectionsInfo for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    return Task.FromResult(
      store.GetCollections().Select(c => store.GetCollectionInfo(c)).AsEnumerable()
    );
  }

  public Task<byte[]> Handle(GetRawContent request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetRawContent for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var result = store.GetContent(request.Collection, request.Key);
    return Task.FromResult(result);
  }

  public Task<IEnumerable<VectorKey>> Handle(GetAllKeys request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetAllKeys for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var result = (store.GetCollectionIndexOf(request.Collection, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();
    return Task.FromResult(result.AsEnumerable());
  }

  public Task<IEnumerable<SearchVectorResultItem>> Handle(SearchVector request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchVector for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var top = request.Top <= 0 ? 10 : request.Top;
    var results = store.Search(request.Collection, request.Embeddings, top, request.Filter)
      .Select(i => new SearchVectorResultItem
      {
        Key = i.entry.Id,
        Content = i.entry.Content.ToArray(),
        Score = i.score
      })
      .AsEnumerable();

    return Task.FromResult(results);
  }
}