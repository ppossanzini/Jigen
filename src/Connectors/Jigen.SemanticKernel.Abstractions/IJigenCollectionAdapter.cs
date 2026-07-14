using System.Linq.Expressions;

namespace Jigen.SemanticKernel.Abstractions;

/// <summary>
/// A single vector-search hit as returned by <see cref="IJigenCollectionAdapter{TRecord}.SearchAsync"/>.
/// </summary>
/// <typeparam name="TRecord">The record's CLR type (its <c>[VectorStoreVector]</c> property is not populated here).</typeparam>
public sealed class JigenSearchHit<TRecord>
  where TRecord : class, new()
{
  /// <summary>Raw entry key, as stored by Jigen (see <see cref="IJigenCollectionAdapter{TRecord}"/> remarks).</summary>
  public required byte[] Key { get; init; }

  /// <summary>The stored content, deserialized. Never carries the vector (Jigen returns content and vector separately).</summary>
  public required TRecord Content { get; init; }

  public required float Score { get; init; }
}

/// <summary>
/// Minimal surface that both the in-process (<c>Jigen.Store</c>) and gRPC
/// (<c>Jigen.Client</c>) collection wrappers implement, so
/// <see cref="JigenVectorStoreCollection{TKey,TRecord}"/> can drive either one
/// without knowing which. Keys travel as raw bytes rather than as either
/// side's own <c>VectorKey</c> type (the in-process and client libraries each
/// define their own, incompatible, <c>VectorKey</c> struct — this interface
/// deliberately depends on neither).
/// </summary>
/// <typeparam name="TRecord">The record's CLR type, as supplied by the caller (must have a public parameterless constructor, same constraint as Jigen's own collections).</typeparam>
public interface IJigenCollectionAdapter<TRecord>
  where TRecord : class, new()
{
  /// <summary>Inserts or overwrites a single entry. <paramref name="content"/> must not carry the vector (stripped by the caller).</summary>
  Task UpsertAsync(byte[] key, TRecord content, float[]? embedding, CancellationToken cancellationToken);

  /// <summary>Bulk insert/overwrite. Implementations may batch this more efficiently than repeated <see cref="UpsertAsync"/> calls.</summary>
  Task UpsertManyAsync(IReadOnlyList<(byte[] Key, TRecord Content, float[]? Embedding)> items, CancellationToken cancellationToken);

  /// <summary>Reads the stored content (without vector) for a key, or <see langword="null"/> if it does not exist.</summary>
  Task<TRecord?> GetContentAsync(byte[] key, CancellationToken cancellationToken);

  /// <summary>Reads the stored full-precision embedding for a key, or <see langword="null"/> if the key does not exist or has no vector.</summary>
  Task<float[]?> GetEmbeddingAsync(byte[] key, CancellationToken cancellationToken);

  /// <summary>Deletes a single entry. Returns whether an entry was actually removed.</summary>
  Task<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken);

  /// <summary>Nearest-neighbour search. <paramref name="filter"/>, when not <see langword="null"/>, is passed straight through to Jigen's own predicate-based filtering (no re-translation).</summary>
  Task<IReadOnlyList<JigenSearchHit<TRecord>>> SearchAsync(float[] vector, int top, Expression<Func<TRecord, bool>>? filter, CancellationToken cancellationToken);

  /// <summary>All keys currently in the collection.</summary>
  Task<IReadOnlyList<byte[]>> GetKeysAsync(CancellationToken cancellationToken);

  /// <summary>Number of entries currently in the collection.</summary>
  Task<int> CountAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Whether the collection currently has any presence in the underlying
  /// store. Jigen collections are created implicitly by the first write, so
  /// this reflects "has something ever been written and not fully cleared",
  /// not a distinct piece of collection metadata.
  /// </summary>
  Task<bool> CollectionExistsAsync(CancellationToken cancellationToken);

  /// <summary>
  /// No-op/idempotent: Jigen has no explicit "create collection" API, a
  /// collection springs into existence on its first write.
  /// </summary>
  Task EnsureCollectionExistsAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Best-effort "delete collection": clears every entry. Jigen has no
  /// separate collection-metadata record to drop, so once the last entry is
  /// removed the collection also disappears from <see cref="CollectionExistsAsync"/>/listings.
  /// </summary>
  Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken);
}
