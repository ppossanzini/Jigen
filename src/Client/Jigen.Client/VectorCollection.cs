using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using Jigen.Client.BaseTypes;
using Jigen.Client.Filtering;
using Jigen.Proto;

namespace Jigen.Client;

public class VectorCollection<T>(Context store, VectorCollectionOptions<T> options = null) :
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  private readonly VectorCollectionOptions<T> _options = options ?? new VectorCollectionOptions<T>();

  public IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> GetEnumerator()
  {
    foreach (var k in Keys)
    {
      if (TryGetValue(k, out var value))
        yield return new KeyValuePair<VectorKey, VectorEntry<T>>(k, value);
    }

    yield break;
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    Add(item.Key, item.Value);
  }

  public void Clear()
  {
    store.ServiceClient.Clear(CollectionKey);
  }

  public async Task ClearAsync(CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.ClearAsync(CollectionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  public bool Contains(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    return ContainsKey(item.Key);
  }

  public void CopyTo(KeyValuePair<VectorKey, VectorEntry<T>>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    return Remove(item.Key);
  }

  public int Count =>
    store.ServiceClient.Count(CollectionKey).Count;

  public async Task<int> CountAsync(CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.CountAsync(CollectionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Count;
  }

  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    store.ServiceClient.SetVector(BuildVector(key, value));
  }

  public void Add(VectorKey key, T content, string sentence)
  {
    store.ServiceClient.SetDocument(BuildDocument(key, content, sentence));
  }

  public void Add(VectorKey key, T content, float[] embeddings)
  {
    this.Add(key, new VectorEntry<T>()
    {
      Content = content, Embedding = embeddings
    });
  }

  public async Task AddAsync(VectorKey key, VectorEntry<T> value, CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.SetVectorAsync(BuildVector(key, value), cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  public async Task AddAsync(VectorKey key, T content, string sentence, CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.SetDocumentAsync(BuildDocument(key, content, sentence), cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  public Task AddAsync(VectorKey key, T content, float[] embeddings, CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embedding = embeddings
    }, cancellationToken);
  }

  private Vector BuildVector(VectorKey key, VectorEntry<T> value)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(value.Content);

    var vector = new Vector()
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(value.Content).Span),
    };

    if (value.Embedding != null)
      vector.Embeddings.AddRange(value.Embedding);

    return vector;
  }

  private Document BuildDocument(VectorKey key, T content, string sentence)
  {
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(sentence);
    ArgumentNullException.ThrowIfNull(content);

    return new Document()
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span),
      Sentence = sentence
    };
  }

  /// <summary>
  /// Bulk insert over a single client-streaming call: one round trip for the
  /// whole batch instead of one per entry. Entries carry their own embeddings
  /// (possibly none). Returns the number of entries accepted by the server.
  /// </summary>
  public async Task<int> AddRangeAsync(IEnumerable<KeyValuePair<VectorKey, VectorEntry<T>>> entries, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entries);

    using var call = store.ServiceClient.SetVectors(cancellationToken: cancellationToken);

    foreach (var entry in entries)
      await call.RequestStream.WriteAsync(BuildVector(entry.Key, entry.Value), cancellationToken).ConfigureAwait(false);

    await call.RequestStream.CompleteAsync().ConfigureAwait(false);
    var result = await call.ResponseAsync.ConfigureAwait(false);
    return result.Accepted;
  }

  /// <summary>
  /// Bulk insert of documents whose embeddings are computed server-side from
  /// the given sentence, batched on the server. Entries with a null or empty
  /// sentence are stored without a vector. Returns the number of entries
  /// accepted by the server.
  /// </summary>
  public async Task<int> AddRangeAsync(IEnumerable<(VectorKey Key, T Content, string Sentence)> entries, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entries);

    using var call = store.ServiceClient.SetDocuments(cancellationToken: cancellationToken);

    foreach (var (key, content, sentence) in entries)
    {
      ArgumentNullException.ThrowIfNull(key.Value);
      ArgumentNullException.ThrowIfNull(content);

      await call.RequestStream.WriteAsync(new Document()
      {
        Database = GetDatabaseName(),
        Collection = GetCollectionName(),
        Key = ByteString.CopyFrom(key.Value),
        Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span),
        Sentence = sentence ?? string.Empty
      }, cancellationToken).ConfigureAwait(false);
    }

    await call.RequestStream.CompleteAsync().ConfigureAwait(false);
    var result = await call.ResponseAsync.ConfigureAwait(false);
    return result.Accepted;
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.ServiceClient.Contains(ToItemKey(key)).Success;
  }

  public async Task<bool> ContainsKeyAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.ContainsAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Success;
  }

  public List<VectorSearchResult<T>> Search(float[] embeddings, int top = 10, SearchOptions options = null)
  {
    return Search(embeddings, predicate: null, top: top, options: options);
  }

  public List<VectorSearchResult<T>> Search(float[] embeddings, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    return MapResults(store.ServiceClient.SearchVector(BuildSearchVectorRequest(embeddings, predicate, top, options)));
  }

  public Task<List<VectorSearchResult<T>>> SearchAsync(float[] embeddings, int top = 10, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchAsync(embeddings, predicate: null, top: top, options: options, cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(float[] embeddings, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var result = await store.ServiceClient.SearchVectorAsync(BuildSearchVectorRequest(embeddings, predicate, top, options), cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  public List<VectorSearchResult<T>> Search(string sentence, int top = 10, SearchOptions options = null)
  {
    return Search(sentence, predicate: null, top: top, options: options);
  }

  public Task<List<VectorSearchResult<T>>> SearchAsync(string sentence, int top = 10, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchAsync(sentence, predicate: null, top: top, options: options, cancellationToken: cancellationToken);
  }

  public List<VectorSearchResult<T>> Search(string sentence, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return MapResults(store.ServiceClient.SearchDocument(BuildSearchDocumentRequest(sentence, predicate, top, options)));
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(string sentence, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    var result = await store.ServiceClient.SearchDocumentAsync(BuildSearchDocumentRequest(sentence, predicate, top, options), cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  private SearchVectorRequest BuildSearchVectorRequest(float[] embeddings, Expression<Func<T, bool>> predicate, int top, SearchOptions options)
  {
    var request = new SearchVectorRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Top = top,
      Tuning = BuildTuning(options)
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    request.Embeddings.AddRange(embeddings);
    return request;
  }

  private SearchDocumentRequest BuildSearchDocumentRequest(string sentence, Expression<Func<T, bool>> predicate, int top, SearchOptions options)
  {
    var request = new SearchDocumentRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Sentence = sentence,
      Top = top,
      Tuning = BuildTuning(options)
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    return request;
  }

  private static SearchTuning BuildTuning(SearchOptions options)
  {
    if (options is null)
      return null;

    var tuning = new SearchTuning
    {
      EfSearch = options.EfSearch,
      NoContent = options.NoContent
    };

    if (options.MinScore.HasValue)
      tuning.MinScore = options.MinScore.Value;

    return tuning;
  }

  private List<VectorSearchResult<T>> MapResults(SearchVectorResponse response)
  {
    return response.Results.Select(i => new VectorSearchResult<T>
    {
      Key = i.Key.ToByteArray(),
      // Empty content = requested with NoContent (or a content-less entry).
      Content = i.Content.IsEmpty ? null : _options.DocumentSerializer.Deserialize<T>(i.Content.Memory),
      Score = i.Score
    }).ToList();
  }

  public bool Remove(VectorKey key)
  {
    return store.ServiceClient.DeleteVector(ToItemKey(key)).Success;
  }

  public async Task<bool> RemoveAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.DeleteVectorAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Success;
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    value = ToEntry(key, store.ServiceClient.GetContent(ToItemKey(key)));
    return value != null;
  }

  /// <summary>
  /// Async counterpart of <see cref="TryGetValue"/>: returns the entry, or
  /// null when the key does not exist.
  /// </summary>
  public async Task<VectorEntry<T>> GetAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetContentAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return ToEntry(key, result);
  }

  private VectorEntry<T> ToEntry(VectorKey key, RawContentResult result)
  {
    if (result.Content.IsEmpty)
      return null;

    return new VectorEntry<T>()
    {
      Key = key,
      Content = _options.DocumentSerializer.Deserialize<T>(result.Content.Memory)
    };
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => TryGetValue(key, out var result) ? result : null;
    set => Add(key, value);
  }

  public ICollection<VectorKey> Keys => store.ServiceClient.GetAllKeys(CollectionKey).Keys.Select(k => (VectorKey)k.Span).ToList();

  public async Task<ICollection<VectorKey>> GetKeysAsync(CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetAllKeysAsync(CollectionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Keys.Select(k => (VectorKey)k.Span).ToList();
  }

  /// <summary>
  /// Streams every key of the collection in chunks: use instead of
  /// <see cref="Keys"/>/<see cref="GetKeysAsync"/> on large collections, where
  /// a single response would exceed the gRPC message size limit.
  /// </summary>
  public async IAsyncEnumerable<VectorKey> StreamKeysAsync(int chunkSize = 0, [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    using var call = store.ServiceClient.StreamKeys(new StreamKeysRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      ChunkSize = chunkSize
    }, cancellationToken: cancellationToken);

    await foreach (var chunk in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
      foreach (var key in chunk.Keys)
        yield return (VectorKey)key.Span;
  }

  public ICollection<VectorEntry<T>> Values =>
    store.ServiceClient.GetAllKeys(CollectionKey).Keys.Select(k => TryGetValue(k.Span, out var value) ? value : null).ToList();


  private ItemKey ToItemKey(VectorKey key) => new()
  {
    Database = GetDatabaseName(),
    Collection = GetCollectionName(),
    Key = ByteString.CopyFrom(key.Value)
  };

  private CollectionKey CollectionKey => new()
  {
    Database = GetDatabaseName(), Collection = GetCollectionName()
  };

  private string GetDatabaseName()
  {
    if (string.IsNullOrWhiteSpace(store.Options.DatabaseName))
      throw new InvalidOperationException(
        "ConnectionOptions.DatabaseName is required and cannot be null or empty.");

    return store.Options.DatabaseName;
  }

  private string GetCollectionName()
  {
    if (string.IsNullOrWhiteSpace(_options.Name))
      throw new InvalidOperationException(
        "VectorCollectionOptions.Name is required and cannot be null or empty.");

    return _options.Name;
  }
}