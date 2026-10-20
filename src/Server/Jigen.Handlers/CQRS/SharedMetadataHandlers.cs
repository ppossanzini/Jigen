using Hikyaku;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

/// <summary>
/// Shared metadata over the remote channels. The metadata content is global to
/// the database; the association is per collection and embedding key. Reads by
/// embedding key keep using GetRawContent, which already returns the shared
/// content.
/// </summary>
public class SharedMetadataHandlers(
  DatabasesManager manager,
  IHikyaku hikyaku,
  IOptions<JigenServerSettings> serverSettings,
  ILogger<SharedMetadataHandlers> logger) :
  IRequestHandler<Core.Command.metadata.CreateMetadata, int>,
  IRequestHandler<Core.Command.metadata.UpdateMetadata, bool>,
  IRequestHandler<Core.Command.metadata.AssociateEmbedding>,
  IRequestHandler<Core.Query.metadata.GetMetadataIndex, int>,
  IRequestHandler<Core.Query.metadata.SearchByMetadata, IEnumerable<Core.Query.metadata.SearchByMetadataResultItem>>
{
  public Task<int> Handle(Core.Command.metadata.CreateMetadata request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing CreateMetadata for db {request.Database}");
    var store = GetStore(request.Database);

    if (request.Content is null)
      throw new ArgumentException("Content is required");

    return Task.FromResult(store.SharedMetadata.AddMetadata(request.Content));
  }

  public Task<bool> Handle(Core.Command.metadata.UpdateMetadata request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing UpdateMetadata for db {request.Database}");
    var store = GetStore(request.Database);

    if (request.Content is null)
      throw new ArgumentException("Content is required");

    return Task.FromResult(store.SharedMetadata.UpdateMetadata(request.MetadataIndex, request.Content));
  }

  public Task Handle(Core.Command.metadata.AssociateEmbedding request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing AssociateEmbedding for db {request.Database}");
    var store = GetStore(request.Database);

    if (!store.TryGetContent(request.Collection, request.Key, out _))
      throw new ArgumentException("Embedding not found");

    try
    {
      store.SharedMetadata.AssociateEmbedding(request.Collection, request.Key, request.MetadataIndex);
    }
    catch (ArgumentOutOfRangeException)
    {
      throw new ArgumentException("Metadata not found");
    }

    return Task.CompletedTask;
  }

  public Task<int> Handle(Core.Query.metadata.GetMetadataIndex request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing GetMetadataIndex for db {request.Database}");
    var store = GetStore(request.Database);

    return Task.FromResult(
      store.SharedMetadata.TryGetMetadataIndex(request.Collection, request.Key, out var metadataIndex)
        ? metadataIndex : -1);
  }

  public async Task<IEnumerable<Core.Query.metadata.SearchByMetadataResultItem>> Handle(
    Core.Query.metadata.SearchByMetadata request, CancellationToken cancellationToken)
  {
    logger.LogDebug($"Executing SearchByMetadata for db {request.Database}");
    var store = GetStore(request.Database);

    if (request.Top <= 0)
      throw new ArgumentException("Top must be greater than zero");
    if (request.CandidateBudget <= 0)
      throw new ArgumentException("CandidateBudget must be greater than zero");

    var embeddings = request.Embeddings;
    if (embeddings is null || embeddings.Length == 0)
    {
      if (string.IsNullOrWhiteSpace(request.Sentence))
        throw new ArgumentException("Provide either embeddings or a sentence");

      embeddings = await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings
      {
        Sentence = request.Sentence,
        Task = serverSettings.Value.EmbeddingQueryTask
      }, cancellationToken);
    }

    var results = store.SearchByMetadata(request.Collection, embeddings,
      request.Top, request.CandidateBudget, request.EfSearch, request.Filter);

    if (request.MinScore.HasValue)
      results = results.Where(i => i.score >= request.MinScore.Value);

    return results
      .Select(i => new Core.Query.metadata.SearchByMetadataResultItem
      {
        MetadataIndex = i.metadataIndex,
        Key = i.entry.Id,
        Content = request.NoContent ? null : i.entry.Content.ToArray(),
        Score = i.score
      })
      .ToArray();
  }

  private Store GetStore(string database)
  {
    if (!manager.ActiveDatabases.TryGetValue(database, out var store))
      throw new ArgumentException("Database not found");
    return store;
  }
}
