using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Abstractions;

/// <summary>
/// Shared <see cref="VectorStoreCollection{TKey,TRecord}"/> implementation for
/// both Jigen connectors (in-process store and gRPC client). All the actual
/// I/O goes through <see cref="IJigenCollectionAdapter{TRecord}"/>, supplied
/// by the concrete subclass; this class owns record&lt;-&gt;storage mapping
/// (via <see cref="JigenRecordModel{TKey,TRecord}"/>) and the parts of the SK
/// contract Jigen has no native equivalent for (filtered non-vector scans,
/// result paging/score-threshold, etc).
/// </summary>
/// <typeparam name="TKey">Record key type; only <see cref="Guid"/> and <see cref="string"/> are supported (see <see cref="JigenRecordModel{TKey,TRecord}"/>).</typeparam>
/// <typeparam name="TRecord">The POCO record type, annotated with <c>Microsoft.Extensions.VectorData</c> attributes.</typeparam>
public abstract class JigenVectorStoreCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
  where TKey : notnull
  where TRecord : class, new()
{
  private readonly IJigenCollectionAdapter<TRecord> _adapter;
  private readonly VectorStoreCollectionMetadata _metadata;
  private readonly JigenRecordModel<TKey, TRecord> _model = JigenRecordModel<TKey, TRecord>.Instance;

  protected JigenVectorStoreCollection(string name, string vectorStoreSystemName, IJigenCollectionAdapter<TRecord> adapter)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(adapter);

    Name = name;
    _adapter = adapter;
    _metadata = new VectorStoreCollectionMetadata
    {
      VectorStoreSystemName = vectorStoreSystemName,
      CollectionName = name
    };
  }

  /// <inheritdoc />
  public override string Name { get; }

  /// <inheritdoc />
  public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    => _adapter.CollectionExistsAsync(cancellationToken);

  /// <inheritdoc />
  public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    => _adapter.EnsureCollectionExistsAsync(cancellationToken);

  /// <inheritdoc />
  public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    => _adapter.EnsureCollectionDeletedAsync(cancellationToken);

  /// <inheritdoc />
  public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(key);

    var keyBytes = _model.KeyToBytes(key);
    var content = await _adapter.GetContentAsync(keyBytes, cancellationToken).ConfigureAwait(false);
    if (content is null)
      return null;

    _model.SetKey(content, key);

    if (options?.IncludeVectors == true)
    {
      var vector = await _adapter.GetEmbeddingAsync(keyBytes, cancellationToken).ConfigureAwait(false);
      _model.SetVector(content, vector);
    }

    return content;
  }

  /// <inheritdoc />
  public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(key);
    return _adapter.DeleteAsync(_model.KeyToBytes(key), cancellationToken);
  }

  /// <inheritdoc />
  public override Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(record);

    var key = _model.KeyToBytes(_model.GetKey(record));
    var vector = _model.GetVector(record);
    var content = _model.StripVectorForStorage(record);

    return _adapter.UpsertAsync(key, content, vector, cancellationToken);
  }

  /// <inheritdoc />
  public override Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(records);

    var items = records
      .Select(record => (
        Key: _model.KeyToBytes(_model.GetKey(record)),
        Content: _model.StripVectorForStorage(record),
        Embedding: _model.GetVector(record)))
      .ToList();

    return _adapter.UpsertManyAsync(items, cancellationToken);
  }

  /// <inheritdoc />
  public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
    TInput searchValue,
    int top,
    VectorSearchOptions<TRecord>? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(searchValue);
    if (top < 1) throw new ArgumentOutOfRangeException(nameof(top));

    options ??= new VectorSearchOptions<TRecord>();

    var vector = ToFloatArray(searchValue);

    // Jigen has no server/index-side Skip: over-fetch and skip locally.
    var fetchTop = top + Math.Max(0, options.Skip);
    var hits = await _adapter.SearchAsync(vector, fetchTop, options.Filter, cancellationToken).ConfigureAwait(false);

    var yielded = 0;
    foreach (var hit in hits.Skip(options.Skip))
    {
      if (yielded >= top)
        yield break;

      if (options.ScoreThreshold is double threshold && hit.Score < threshold)
        continue;

      _model.SetKey(hit.Content, _model.KeyFromBytes(hit.Key));

      if (options.IncludeVectors)
      {
        var embedding = await _adapter.GetEmbeddingAsync(hit.Key, cancellationToken).ConfigureAwait(false);
        _model.SetVector(hit.Content, embedding);
      }

      yielded++;
      yield return new VectorSearchResult<TRecord>(hit.Content, hit.Score);
    }
  }

  /// <inheritdoc />
  /// <remarks>
  /// Jigen has no "scan by predicate" primitive independent of a vector
  /// query, so this reads every key and applies <paramref name="filter"/>
  /// client-side. Fine for small/medium collections; do not rely on it for
  /// large ones.
  /// </remarks>
  public override async IAsyncEnumerable<TRecord> GetAsync(
    System.Linq.Expressions.Expression<Func<TRecord, bool>> filter,
    int top,
    FilteredRecordRetrievalOptions<TRecord>? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(filter);
    if (top < 1) throw new ArgumentOutOfRangeException(nameof(top));

    options ??= new FilteredRecordRetrievalOptions<TRecord>();
    var predicate = filter.Compile();

    var keys = await _adapter.GetKeysAsync(cancellationToken).ConfigureAwait(false);

    var matches = new List<TRecord>();
    foreach (var keyBytes in keys)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var content = await _adapter.GetContentAsync(keyBytes, cancellationToken).ConfigureAwait(false);
      if (content is null)
        continue;

      _model.SetKey(content, _model.KeyFromBytes(keyBytes));

      if (options.IncludeVectors)
      {
        var embedding = await _adapter.GetEmbeddingAsync(keyBytes, cancellationToken).ConfigureAwait(false);
        _model.SetVector(content, embedding);
      }

      if (predicate(content))
        matches.Add(content);
    }

    IEnumerable<TRecord> ordered = matches;

    var orderBy = options.OrderBy?.Invoke(new()).Values;
    if (orderBy is { Count: > 0 })
    {
      var first = orderBy[0];
      var sorted = first.Ascending
        ? matches.AsQueryable().OrderBy(first.PropertySelector)
        : matches.AsQueryable().OrderByDescending(first.PropertySelector);

      for (var i = 1; i < orderBy.Count; i++)
      {
        var next = orderBy[i];
        sorted = next.Ascending ? sorted.ThenBy(next.PropertySelector) : sorted.ThenByDescending(next.PropertySelector);
      }

      ordered = sorted;
    }

    foreach (var record in ordered.Skip(options.Skip).Take(top))
      yield return record;
  }

  /// <inheritdoc />
  public override object? GetService(Type serviceType, object? serviceKey = null)
  {
    ArgumentNullException.ThrowIfNull(serviceType);

    return
      serviceKey is not null ? null :
      serviceType == typeof(VectorStoreCollectionMetadata) ? _metadata :
      serviceType.IsInstanceOfType(this) ? this :
      null;
  }

  /// <summary>
  /// Converts a <see cref="SearchAsync{TInput}"/> input to a plain vector.
  /// Only pre-computed vectors are supported (<see cref="float"/>[],
  /// <see cref="ReadOnlyMemory{T}"/>, <see cref="Embedding{T}"/>) — this
  /// connector does not wire up an <c>IEmbeddingGenerator</c> (out of scope
  /// for v1, see project notes); callers must generate embeddings upstream.
  /// </summary>
  private static float[] ToFloatArray<TInput>(TInput searchValue)
  {
    return searchValue switch
    {
      float[] array => array,
      ReadOnlyMemory<float> memory => memory.ToArray(),
      Embedding<float> embedding => embedding.Vector.ToArray(),
      _ => throw new NotSupportedException(
        $"Jigen's Semantic Kernel connector only accepts a pre-computed vector as the search value " +
        $"(float[], ReadOnlyMemory<float> or Embedding<float>); got '{typeof(TInput)}'. " +
        "Generate the embedding upstream (e.g. via IEmbeddingGenerator<string, Embedding<float>>) before calling SearchAsync.")
    };
  }
}
