using Hikyaku;
using Jigen.Core.Command.collections;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class CollectionCommandHandlers(
  DatabasesManager manager,
  ILogger<CollectionCommandHandlers> logger,
  Jigen.SemanticTools.IEmbeddingGenerator embeddingGenerator
) :
  IRequestHandler<Core.Command.collections.AppendDocument>,
  IRequestHandler<Core.Command.collections.SetDocument>,
  IRequestHandler<Core.Command.collections.AppendVector>,
  IRequestHandler<Core.Command.collections.DeleteVector>,
  IRequestHandler<Core.Command.collections.SetVector>,
  IRequestHandler<Core.Command.collections.SetRawVector>

{
  public async Task Handle(AppendDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var content = request.Content != null ? store.GetSerializer(request.Content.GetType()).Serialize(request.Content) : null;
    float[] embeddings = request.Sentence != null ? embeddingGenerator.GenerateEmbedding(request.Sentence) : null;

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = content,
        Embedding = embeddings
      });
  }

  public async Task Handle(SetDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var content = request.Content != null ? store.GetSerializer(request.Content.GetType()).Serialize(request.Content) : null;
    var embeddings = request.Sentence != null ? embeddingGenerator.GenerateEmbedding(request.Sentence) : null;

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = content,
        Embedding = embeddings
      });
  }

  public async Task Handle(AppendVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var content = request.Content != null ? store.GetSerializer(request.Content.GetType()).Serialize(request.Content) : null;

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = content,
        Embedding = request.Embeddings
      });
  }

  public async Task Handle(DeleteVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.DeleteContent(request.Collection, request.Key);
  }

  public async Task Handle(SetVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var content = request.Content != null ? store.GetSerializer(request.Content.GetType()).Serialize(request.Content) : null;

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = content,
        Embedding = request.Embeddings
      });
  }

  public async Task Handle(SetRawVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = request.Content,
        Embedding = request.Embeddings
      });
  }
}