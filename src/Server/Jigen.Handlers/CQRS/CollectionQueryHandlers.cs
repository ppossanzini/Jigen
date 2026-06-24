using Hikyaku;
using System.Text.Json;
using System.Diagnostics;
using Jigen.Core.Dto.collections;
using Jigen.Core.Query.collections;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Jigen.Indexers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Jigen.SemanticTools;

namespace Jigen.Handlers.CQRS;

public class CollectionQueryHandlers(
  DatabasesManager manager,
  // DatabaseOwnershipGuard ownershipGuard,
  IDocumentSerializer serializer,
  IEmbeddingGenerator embeddingGenerator,
  ILogger<CollectionCommandHandlers> logger) :
  IRequestHandler<Core.Query.collections.ListCollections, IEnumerable<string>>,
  IRequestHandler<Core.Query.collections.GetCollectionInfo, CollectionInfo>,
  IRequestHandler<Core.Query.collections.GetCollectionsInfo, IEnumerable<CollectionInfo>>,
  IRequestHandler<Core.Query.collections.GetRawContent, byte[]>,
  IRequestHandler<Core.Query.collections.GetAllKeys, IEnumerable<VectorKey>>, 
  IRequestHandler<Core.Query.collections.SearchVector, IEnumerable<SearchVectorResultItem>>,
  IRequestHandler<Core.Query.collections.SearchCollections, SearchCollectionsResult>

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

    var result = (store.GetCollectionIndexOf(request.Collection, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ??
                 Array.Empty<VectorKey>();
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

  public async Task<SearchCollectionsResult> Handle(SearchCollections request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchCollections for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    if (request.Data == null) throw new ArgumentException("Request payload is required");

    var collections = (request.Data.Collections ?? Array.Empty<string>())
      .Where(c => !string.IsNullOrWhiteSpace(c))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    if (collections.Length == 0)
      throw new ArgumentException("At least one collection is required");

    var embeddingsTimer = new Stopwatch();
    var embeddings = request.Data.Embeddings;
    if (request.Data.Embeddings == null || request.Data.Embeddings.Length == 0)
    {
      if (string.IsNullOrWhiteSpace(request.Data.Sentence))
        throw new ArgumentException("Provide a sentence or embeddings");

      embeddingsTimer.Start();
      embeddings = embeddingGenerator.GenerateEmbedding(request.Data.Sentence);
      embeddingsTimer.Stop();
    }
    var top = request.Data.Top <= 0 ? 10 : request.Data.Top;
    var searchTimer = Stopwatch.StartNew();
    var collectionsResults = new CollectionSearchResult[collections.Length];

    Parallel.For(0, collections.Length, (i, ct) =>
    {
      var collection = collections[i];
      var collectionTimer = Stopwatch.StartNew();
      var resultItems = store.Search(collection, embeddings, top)
        .Select(r => new CollectionSearchResultItem
        {
          Key = r.entry.Id,
          Content = serializer.ToJsonObject(r.entry.Content),
          Score = r.score
        })
        .ToArray();
      collectionTimer.Stop();

      collectionsResults[i] = new CollectionSearchResult
      {
        Collection = collection,
        SearchTime = collectionTimer.Elapsed.TotalMilliseconds,
        Results = resultItems
      };
    });

    searchTimer.Stop();

    var mergeTimer = Stopwatch.StartNew();
    var mergedResults = collectionsResults.SelectMany(c => c.Results).ToArray();
    mergeTimer.Stop();

    var sortingTimer = Stopwatch.StartNew();
    var sortedResults = mergedResults
      .OrderByDescending(i => i.Score)
      .Take(top * collections.Length)
      .ToArray();
    sortingTimer.Stop();

    return new SearchCollectionsResult
    {
      EmbeddingsCalculationTime = embeddingsTimer.Elapsed.TotalMilliseconds,
      SearchTime = searchTimer.Elapsed.TotalMilliseconds,
      MergeTime = mergeTimer.Elapsed.TotalMilliseconds,
      SortingTime = sortingTimer.Elapsed.TotalMilliseconds,
      CollectionsResults = collectionsResults,
      MergedResults = sortedResults
    };
  }
}