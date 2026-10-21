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

namespace Jigen.Handlers.CQRS;

public class CollectionQueryHandlers(
  DatabasesManager manager,
  // DatabaseOwnershipGuard ownershipGuard,
  IDocumentSerializer serializer,
  IHikyaku hikyaku,
  ILogger<CollectionCommandHandlers> logger) :
  IRequestHandler<Core.Query.collections.ListCollections, IEnumerable<string>>,
  IRequestHandler<Core.Query.collections.GetCollectionInfo, CollectionInfo>,
  IRequestHandler<Core.Query.collections.GetCollectionsInfo, IEnumerable<CollectionInfo>>,
  IRequestHandler<Core.Query.collections.GetRawContent, byte[]>,
  IRequestHandler<Core.Query.collections.GetEmbedding, float[]>,
  IRequestHandler<Core.Query.collections.GetAllKeys, IEnumerable<VectorKey>>,
  IRequestHandler<Core.Query.collections.SearchVector, IEnumerable<SearchVectorResultItem>>,
  IRequestHandler<Core.Query.collections.SearchCollections, SearchCollectionsResult>,
  IRequestHandler<Core.Query.collections.GetSparse, SparseVector>,
  IRequestHandler<Core.Query.collections.SearchSparse, IEnumerable<SearchVectorResultItem>>,
  IRequestHandler<Core.Query.collections.SearchSparseByMetadata, IEnumerable<SearchSparseByMetadataResultItem>>,
  IRequestHandler<Core.Query.collections.SearchHybrid, IEnumerable<SearchVectorResultItem>>

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

  public Task<float[]> Handle(GetEmbedding request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetEmbedding for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    return Task.FromResult(store.GetEmbedding(request.Collection, request.Key));
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
    var results = store.Search(request.Collection, request.Embeddings, top, request.EfSearch, request.Filter);

    if (request.MinScore.HasValue)
      results = results.Where(i => i.score >= request.MinScore.Value);

    return Task.FromResult(results
      .Select(i => new SearchVectorResultItem
      {
        Key = i.entry.Id,
        Content = request.NoContent ? null : i.entry.Content.ToArray(),
        Score = i.score
      })
      .AsEnumerable());
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
      embeddings = await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings
      {
        Sentence = request.Data.Sentence,
        Task = "search_query"
      }, cancellationToken);
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

  // ── Sparse slot ──

  public Task<SparseVector> Handle(Core.Query.collections.GetSparse request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetSparse for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    return Task.FromResult(store.GetSparse(request.Collection, request.Key));
  }

  public Task<IEnumerable<SearchVectorResultItem>> Handle(Core.Query.collections.SearchSparse request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchSparse for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var query = RequireSparseQuery(request.Sparse);
    var top = request.Top <= 0 ? 10 : request.Top;
    var results = store.SearchSparse(request.Collection, query, top, request.Filter);

    if (request.MinScore.HasValue)
      results = results.Where(i => i.score >= request.MinScore.Value);

    return Task.FromResult(results
      .Select(i => new SearchVectorResultItem
      {
        Key = i.key,
        Content = request.NoContent ? null : i.content,
        Score = i.score,
        InDense = false,
        InSparse = true,
        DenseScore = 0f,
        SparseScore = i.score
      })
      .AsEnumerable());
  }

  public Task<IEnumerable<SearchSparseByMetadataResultItem>> Handle(
    Core.Query.collections.SearchSparseByMetadata request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchSparseByMetadata for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var query = RequireSparseQuery(request.Sparse);
    if (request.CandidateBudget <= 0)
      throw new ArgumentException("CandidateBudget must be greater than zero.", nameof(request));

    var top = request.Top <= 0 ? 10 : request.Top;
    var results = store.SearchSparseByMetadata(
      request.Collection, query, top, request.CandidateBudget, request.Filter);

    return Task.FromResult(results
      .Select(i => new SearchSparseByMetadataResultItem
      {
        MetadataIndex = i.metadataIndex,
        Key = i.key,
        Content = request.NoContent ? null : i.content,
        Score = i.score
      })
      .AsEnumerable());
  }

  public Task<IEnumerable<SearchVectorResultItem>> Handle(Core.Query.collections.SearchHybrid request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchHybrid for db {request.Database}");
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    if (request.Embeddings is not { Length: > 0 } && request.Sparse is null)
      throw new ArgumentException("A hybrid search needs at least one of the two vectors.", nameof(request));

    var sparse = request.Sparse is null ? null : RequireSparseQuery(request.Sparse);
    var top = request.Top <= 0 ? 10 : request.Top;
    var results = store.SearchHybrid(request.Collection, request.Embeddings, sparse, top,
      request.DenseWeight, request.SparseWeight, request.Filter);

    return Task.FromResult(results
      .Select(i => new SearchVectorResultItem
      {
        Key = i.entry.Id,
        Content = request.NoContent ? null : i.entry.Content.ToArray(),
        Score = (float)i.score,
        InDense = i.inDense,
        InSparse = i.inSparse,
        DenseScore = i.denseScore,
        SparseScore = i.sparseScore
      })
      .AsEnumerable());
  }

  /// <summary>
  /// A lexical query needs terms: an absent vector is a caller mistake, not an
  /// empty result. The shape (ordering, signs, finiteness) is validated when
  /// the vector is built, so an instance here is already well formed.
  /// </summary>
  private static SparseVector RequireSparseQuery(SparseVector sparse) =>
    sparse is { Count: > 0 }
      ? sparse
      : throw new ArgumentException("A lexical search needs at least one term.", nameof(sparse));
}
