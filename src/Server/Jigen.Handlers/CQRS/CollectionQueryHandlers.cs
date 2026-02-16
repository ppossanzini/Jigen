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
  IDocumentSerializer serializer,
  ILogger<CollectionCommandHandlers> logger) :
  IRequestHandler<Core.Query.collections.ListCollections, IEnumerable<string>>,
  IRequestHandler<Core.Query.collections.GetCollectionInfo, CollectionInfo>,
  IRequestHandler<Core.Query.collections.GetCollectionsInfo, IEnumerable<CollectionInfo>>,
  IRequestHandler<Core.Query.collections.GetContent, object>,
  IRequestHandler<Core.Query.collections.GetRawContent, byte[]>,
  IRequestHandler<Core.Query.collections.GetAllKeys, IEnumerable<VectorKey>>

{
  public Task<IEnumerable<string>> Handle(ListCollections request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(store.GetCollections().AsEnumerable());
  }

  public Task<CollectionInfo> Handle(GetCollectionInfo request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(store.GetCollectionInfo(request.Collection));
  }

  public Task<IEnumerable<CollectionInfo>> Handle(GetCollectionsInfo request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    return Task.FromResult(
      store.GetCollections().Select(c => store.GetCollectionInfo(c)).AsEnumerable()
    );
  }

  public Task<object> Handle(GetContent request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var result = store.GetContent(request.Collection, request.Key);
    return Task.FromResult(serializer.Deserialize(request.ResultType, result));
  }

  public Task<byte[]> Handle(GetRawContent request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var result = store.GetContent(request.Collection, request.Key);
    return Task.FromResult(result);
  }

  public Task<IEnumerable<VectorKey>> Handle(GetAllKeys request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var result = (store.PositionIndex.TryGetValue(request.Collection, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();
    return Task.FromResult(result.AsEnumerable());
  }
}