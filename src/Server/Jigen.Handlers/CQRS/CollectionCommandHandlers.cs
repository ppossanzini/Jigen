using Hikyaku;
using Jigen.Core.Command.collections;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class CollectionCommandHandlers( DatabasesManager manager , IHikyaku hikyaku) :
  IRequestHandler<Core.Command.collections.AppendDocument>,
  IRequestHandler<Core.Command.collections.SetDocument>,
  IRequestHandler<Core.Command.collections.AppendVector>,
  IRequestHandler<Core.Command.collections.DeleteVector>,
  IRequestHandler<Core.Command.collections.SetVector>,
  IRequestHandler<Core.Command.collections.SetRawVector>,
  IRequestHandler<Core.Command.collections.Clear>,
  IRequestHandler<Core.Command.collections.Count, int>,
  IRequestHandler<Core.Command.collections.Contains, bool>

{
  public async Task Handle(AppendDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    float[] embeddings = request.Embeddings ?? ( request.Sentence != null
      ? await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence }, cancellationToken)
      : null);

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = request.Content,
        Embedding = embeddings
      });
  }

  public async Task Handle(SetDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var embeddings = request.Embeddings ?? ( request.Sentence != null
      ? await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence }, cancellationToken)
      : null);

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = request.Content,
        Embedding = embeddings
      });
  }

  public async Task Handle(AppendVector request, CancellationToken cancellationToken)
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

  public async Task Handle(DeleteVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.DeleteContent(request.Collection, request.Key);
  }

  public async Task Handle(SetVector request, CancellationToken cancellationToken)
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

  public async Task Handle(Clear request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.ClearContent(request.Collection);
  }

  public Task<int> Handle(Count request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(
      store.GetCollectionIndexOf(request.Collection, out var index) ? index.Count : 0);
  }

  public Task<bool> Handle(Contains request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");
    return Task.FromResult(
      store.GetCollectionIndexOf(request.Collection, out var index) && index.ContainsKey(request.Key)
    );
  }
}