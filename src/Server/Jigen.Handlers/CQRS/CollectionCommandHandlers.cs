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

    var (embeddings, sparse) = await ResolveVectors(
      store, request.Embeddings, request.Sparse, request.Sentence, cancellationToken);

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = request.Content,
        Embedding = embeddings,
        Sparse = sparse
      });
  }

  public async Task Handle(SetDocument request, CancellationToken cancellationToken)
  {
    if (!manager.ActiveDatabases.TryGetValue(request.Database, out var store)) throw new ArgumentException("Database not found");

    var (embeddings, sparse) = await ResolveVectors(
      store, request.Embeddings, request.Sparse, request.Sentence, cancellationToken);

    await store.AppendContent(
      new VectorEntry()
      {
        CollectionName = request.Collection,
        Id = request.Key,
        Content = request.Content,
        Embedding = embeddings,
        Sparse = sparse
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
        Embedding = request.Embeddings,
        Sparse = request.Sparse
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
        Embedding = request.Embeddings,
        Sparse = request.Sparse
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
        Embedding = request.Embeddings,
        Sparse = request.Sparse
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

  /// <summary>
  /// Resolves the two vector slots of a write. A sentence the caller supplied is
  /// the one input that fills both: the dense embedding and the sparse lexical
  /// vector are generated in one dispatch, and a slot the caller gave explicitly
  /// always wins over the generated one. Without a sentence nothing is generated,
  /// so an entry with only content stays exactly that.
  /// </summary>
  private async Task<(float[] embeddings, SparseVector sparse)> ResolveVectors(
    Store store, float[] embeddings, SparseVector sparse, string sentence, CancellationToken cancellationToken)
  {
    var needsDense = embeddings is not { Length: > 0 };
    var needsSparse = sparse is null;

    if (string.IsNullOrWhiteSpace(sentence) || (!needsDense && !needsSparse))
      return (embeddings, sparse);

    var pair = await hikyaku.Send(new Embedding.Core.Commands.CalculateVectorPair
    {
      Sentence = sentence,
      Task = serverSettings.Value.EmbeddingCostructionTask
    }, cancellationToken);

    // A generated sparse vector is still a sparse write: without the log it
    // could not be recovered after a crash. Say so here, because the caller
    // asked for a sentence, not for a lexical slot, and the store's own message
    // would not explain where the vector came from.
    if (needsSparse && pair.Sparse?.Terms is { Length: > 0 } && store.Options.Wal?.Enabled != true)
      throw new InvalidOperationException(
        "The sentence generates a sparse vector, and writing one requires the write-ahead log: " +
        "set JigenServer:Wal:Enabled (or send the dense embedding explicitly) to write without it.");

    return (
      needsDense ? pair.Dense : embeddings,
      needsSparse ? ToSparseVector(pair.Sparse) : sparse);
  }

  /// <summary>
  /// An empty sparse vector means the text produced no terms: the slot is then
  /// absent, not empty, because the store refuses an empty vector.
  /// </summary>
  private static SparseVector ToSparseVector(Embedding.Core.Dto.SparseEmbeddingDto sparse)
  {
    if (sparse?.Terms is not { Length: > 0 }) return null;
    if (sparse.Terms.Length != sparse.Weights.Length) return null;

    return SparseVector.Create(sparse.Terms, sparse.Weights, int.MaxValue);
  }
}
