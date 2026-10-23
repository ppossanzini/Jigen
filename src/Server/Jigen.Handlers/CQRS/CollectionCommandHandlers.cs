using Hikyaku;
using Jigen.Core.Command.collections;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.CQRS;

public class CollectionCommandHandlers( DatabasesManager manager , IHikyaku hikyaku, IOptions<JigenServerSettings> serverSettings) :
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

    float[] embeddings = request.Vectors is { Length: > 0 }
      ? null
      : NullIfEmpty(request.Embeddings) ?? ( request.Sentence != null
        ? await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence, Task = serverSettings.Value.EmbeddingCostructionTask }, cancellationToken)
        : null);

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, embeddings, request.Vectors));
  }

  public async Task Handle(SetDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var embeddings = request.Vectors is { Length: > 0 }
      ? null
      : NullIfEmpty(request.Embeddings) ?? ( request.Sentence != null
        ? await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence, Task = serverSettings.Value.EmbeddingCostructionTask  }, cancellationToken)
        : null);

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, embeddings, request.Vectors));
  }

  public async Task Handle(AppendVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Embeddings, request.Vectors));
  }

  public async Task Handle(DeleteVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.DeleteContent(request.Collection, request.Key);
  }

  public async Task Handle(SetVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Embeddings, request.Vectors));
  }

  public async Task Handle(SetRawVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Embeddings, request.Vectors));
  }

  public async Task Handle(Clear request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.ClearContent(request.Collection);
  }

  private static VectorEntry ToEntry(string collection, byte[] key, byte[] content, float[] embeddings, float[][] vectors)
  {
    var entry = new VectorEntry
    {
      CollectionName = collection,
      Id = key,
      Content = content
    };

    if (vectors is { Length: > 0 })
      entry.Embeddings = vectors.Select(v => (ReadOnlyMemory<float>)v).ToArray();
    else
      entry.Embedding = embeddings;

    return entry;
  }

  private static float[] NullIfEmpty(float[] embeddings) =>
    embeddings is { Length: > 0 } ? embeddings : null;

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