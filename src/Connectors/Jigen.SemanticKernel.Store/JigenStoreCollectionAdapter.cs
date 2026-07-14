using System.Linq.Expressions;
using Jigen;
using Jigen.DataStructures;
using Jigen.SemanticKernel.Abstractions;

namespace Jigen.SemanticKernel.Store;

/// <summary>
/// Drives a single in-process <see cref="VectorCollection{T}"/> on behalf of
/// <see cref="JigenVectorStoreCollection{TKey,TRecord}"/>.
/// </summary>
internal sealed class JigenStoreCollectionAdapter<TRecord> : IJigenCollectionAdapter<TRecord>
  where TRecord : class, new()
{
  private readonly global::Jigen.Store _store;
  private readonly VectorCollection<TRecord> _collection;
  private readonly string _collectionName;

  public JigenStoreCollectionAdapter(global::Jigen.Store store, string collectionName)
  {
    _store = store;
    _collectionName = collectionName;
    _collection = new VectorCollection<TRecord>(store, new VectorCollectionOptions<TRecord> { Name = collectionName });
  }

  public async Task UpsertAsync(byte[] key, TRecord content, float[]? embedding, CancellationToken cancellationToken)
  {
    await _collection.AddAsync((VectorKey)key, content, embedding ?? []).ConfigureAwait(false);

    // AppendContent only enqueues (see StoreWritingExtensions.AppendContent):
    // without this, a GetAsync/SearchAsync right after UpsertAsync could race
    // the background writer and index workers and miss the entry. SK's
    // VectorStoreCollection contract expects UpsertAsync to make the record
    // immediately visible, so drain the queue before returning.
    await _store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  public async Task UpsertManyAsync(IReadOnlyList<(byte[] Key, TRecord Content, float[]? Embedding)> items, CancellationToken cancellationToken)
  {
    // Jigen.Store's VectorCollection has no bulk-insert API (unlike the gRPC
    // client's AddRangeAsync): entries go through the same background
    // ingestion queue either way, so sequential AddAsync calls are as fast as
    // anything we could build here without touching Store internals.
    foreach (var (key, content, embedding) in items)
    {
      cancellationToken.ThrowIfCancellationRequested();
      await _collection.AddAsync((VectorKey)key, content, embedding ?? []).ConfigureAwait(false);
    }

    // One drain for the whole batch (see UpsertAsync for why this is needed).
    await _store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  public Task<TRecord?> GetContentAsync(byte[] key, CancellationToken cancellationToken)
  {
    return Task.FromResult(_collection.TryGetValue((VectorKey)key, out var entry) ? entry.Content : null);
  }

  public Task<float[]?> GetEmbeddingAsync(byte[] key, CancellationToken cancellationToken)
    => Task.FromResult<float[]?>(_collection.GetEmbedding((VectorKey)key));

  public Task<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken)
    => _collection.RemoveAsync((VectorKey)key);

  public Task<IReadOnlyList<JigenSearchHit<TRecord>>> SearchAsync(float[] vector, int top, Expression<Func<TRecord, bool>>? filter, CancellationToken cancellationToken)
  {
    var results = _collection.Search(vector, top, filter);

    IReadOnlyList<JigenSearchHit<TRecord>> hits = results
      .Where(r => r.Content is not null)
      .Select(r => new JigenSearchHit<TRecord>
      {
        Key = r.Key.Value,
        Content = r.Content!,
        Score = r.Score
      })
      .ToList();

    return Task.FromResult(hits);
  }

  public Task<IReadOnlyList<byte[]>> GetKeysAsync(CancellationToken cancellationToken)
    => Task.FromResult<IReadOnlyList<byte[]>>(_collection.Keys.Select(k => k.Value).ToList());

  public Task<int> CountAsync(CancellationToken cancellationToken)
    => Task.FromResult(_collection.Count);

  public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken)
    => Task.FromResult(_store.GetCollections().Contains(_collectionName));

  public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    // Jigen creates collections implicitly on first write; nothing to do up front.
    => Task.CompletedTask;

  public Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken)
    // No standalone "collection" record to drop: clearing every entry also
    // removes the collection's key from Store.PositionIndex (see
    // StoreWritingExtensions.ClearContent), so it stops showing up in
    // GetCollections()/CollectionExistsAsync too.
    => _collection.ClearAsync();
}
