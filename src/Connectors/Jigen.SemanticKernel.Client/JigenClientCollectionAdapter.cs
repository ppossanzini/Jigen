using System.Linq.Expressions;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;
using Jigen.SemanticKernel.Abstractions;

namespace Jigen.SemanticKernel.Client;

/// <summary>
/// Drives a single gRPC <see cref="VectorCollection{T}"/> on behalf of
/// <see cref="JigenVectorStoreCollection{TKey,TRecord}"/>.
/// </summary>
internal sealed class JigenClientCollectionAdapter<TRecord> : IJigenCollectionAdapter<TRecord>
  where TRecord : class, new()
{
  private readonly Context _context;
  private readonly VectorCollection<TRecord> _collection;
  private readonly string _collectionName;

  public JigenClientCollectionAdapter(Context context, string collectionName)
  {
    _context = context;
    _collectionName = collectionName;
    _collection = new VectorCollection<TRecord>(context, new VectorCollectionOptions<TRecord> { Name = collectionName });
  }

  public Task UpsertAsync(byte[] key, TRecord content, float[]? embedding, CancellationToken cancellationToken)
    => _collection.AddAsync((VectorKey)key, content, embedding ?? [], cancellationToken);

  public async Task UpsertManyAsync(IReadOnlyList<(byte[] Key, TRecord Content, float[]? Embedding)> items, CancellationToken cancellationToken)
  {
    // Unlike the in-process adapter, the gRPC client has a real bulk path
    // (one client-streaming call for the whole batch): use it.
    var entries = items.Select(item => new KeyValuePair<VectorKey, VectorEntry<TRecord>>(
      (VectorKey)item.Key,
      new VectorEntry<TRecord> { Content = item.Content, Embedding = item.Embedding ?? [] }));

    await _collection.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
  }

  public async Task<TRecord?> GetContentAsync(byte[] key, CancellationToken cancellationToken)
  {
    var entry = await _collection.GetAsync((VectorKey)key, cancellationToken).ConfigureAwait(false);
    return entry?.Content;
  }

  public Task<float[]?> GetEmbeddingAsync(byte[] key, CancellationToken cancellationToken)
    => _collection.GetEmbeddingAsync((VectorKey)key, cancellationToken);

  public Task<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken)
    => _collection.RemoveAsync((VectorKey)key, cancellationToken);

  public async Task<IReadOnlyList<JigenSearchHit<TRecord>>> SearchAsync(float[] vector, int top, Expression<Func<TRecord, bool>>? filter, CancellationToken cancellationToken)
  {
    var results = filter is not null
      ? await _collection.SearchAsync(vector, filter, top, null, cancellationToken).ConfigureAwait(false)
      : await _collection.SearchAsync(vector, top, null, cancellationToken).ConfigureAwait(false);

    IReadOnlyList<JigenSearchHit<TRecord>> hits = results
      .Where(r => r.Content is not null)
      .Select(r => new JigenSearchHit<TRecord>
      {
        Key = r.Key.Value,
        Content = r.Content!,
        Score = r.Score
      })
      .ToList();

    return hits;
  }

  public async Task<IReadOnlyList<byte[]>> GetKeysAsync(CancellationToken cancellationToken)
  {
    var keys = await _collection.GetKeysAsync(cancellationToken).ConfigureAwait(false);
    return keys.Select(k => k.Value).ToList();
  }

  public Task<int> CountAsync(CancellationToken cancellationToken)
    => _collection.CountAsync(cancellationToken);

  public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken)
  {
    var names = await ListCollectionNamesAsync(cancellationToken).ConfigureAwait(false);
    return names.Contains(_collectionName);
  }

  public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    // Jigen creates collections implicitly on first write; nothing to do up front.
    => Task.CompletedTask;

  public async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken)
    // Best-effort, same caveat as the in-process adapter: this clears every
    // entry (server-side Clear tombstones the whole collection), there is no
    // separate collection-metadata record to drop.
    => await _collection.ClearAsync(cancellationToken).ConfigureAwait(false);

  private async Task<IReadOnlyList<string>> ListCollectionNamesAsync(CancellationToken cancellationToken)
  {
    var databaseName = _context.Options.DatabaseName;
    ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

    var result = await _context.ServiceClient
      .ListCollectionsAsync(new CollectionKey { Database = databaseName }, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    return result.Collections;
  }
}
