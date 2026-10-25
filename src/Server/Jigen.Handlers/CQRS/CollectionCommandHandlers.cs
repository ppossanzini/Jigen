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

    var vectors = request.Vectors is { Length: > 0 }
      ? request.Vectors
      : request.Sentence is not null
        ? [await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence, Task = serverSettings.Value.EmbeddingCostructionTask }, cancellationToken)]
        : null;

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, vectors,
      await ResolveSparseTerms(request.SparseTerms, request.SparseFromSentence, request.Sentence, cancellationToken)));
  }

  public async Task Handle(SetDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var vectors = request.Vectors is { Length: > 0 }
      ? request.Vectors
      : request.Sentence is not null
        ? [await hikyaku.Send(new Embedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence, Task = serverSettings.Value.EmbeddingCostructionTask  }, cancellationToken)]
        : null;

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, vectors,
      await ResolveSparseTerms(request.SparseTerms, request.SparseFromSentence, request.Sentence, cancellationToken)));
  }

  public async Task Handle(AppendVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Vectors,
      request.SparseVectors));
  }

  public async Task Handle(DeleteVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.DeleteContent(request.Collection, request.Key);
  }

  public async Task Handle(SetVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Vectors,
      request.SparseVectors));
  }

  public async Task Handle(SetRawVector request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.AppendContent(ToEntry(request.Collection, request.Key, request.Content, request.Vectors,
      request.SparseVectors));
  }

  public async Task Handle(Clear request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    await store.ClearContent(request.Collection);
  }

  /// <summary>
  /// Sparse terms of a document: the ones the caller sent, or the ones derived
  /// from its sentence by the same tokenizer that embeds it. Null means the entry
  /// carries no sparse vector.
  /// </summary>
  private async Task<IReadOnlyList<SparseTerm[]>> ResolveSparseTerms(SparseTerm[] explicitTerms,
    bool fromSentence, string sentence, CancellationToken cancellationToken)
  {
    if (explicitTerms is not null)
      return [explicitTerms];
    if (!fromSentence || string.IsNullOrWhiteSpace(sentence))
      return null;

    var sparse = await hikyaku.Send(new Embedding.Core.Commands.CalculateSparseEmbedding
    {
      Sentence = sentence
    }, cancellationToken);

    if (sparse.Terms.Length == 0)
      return null;

    var terms = new SparseTerm[sparse.Terms.Length];
    for (var i = 0; i < terms.Length; i++)
      terms[i] = new SparseTerm(sparse.Terms[i], sparse.Weights[i]);
    return [terms];
  }

  private static VectorEntry ToEntry(string collection, byte[] key, byte[] content, float[][] vectors,
    IReadOnlyList<SparseTerm[]> sparseVectors = null) =>
    new()
    {
      CollectionName = collection,
      Id = key,
      Content = content,
      Embeddings = vectors is { Length: > 0 }
        ? vectors.Select(v => (ReadOnlyMemory<float>)v).ToArray()
        : [],
      SparseVectors = sparseVectors ?? []
    };

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