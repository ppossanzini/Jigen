using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using Jigen.Client.BaseTypes;
using Jigen.Client.Filtering;
using Jigen.Proto;
// Both the wire contract and this client define a sparse vector: the aliases
// keep every call site explicit about which one it holds.
using SparseVector = Jigen.Client.BaseTypes.SparseVector;
using ProtoSparseVector = Jigen.Proto.SparseVector;

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

  public void Add(VectorEntry<T> value)
  {
    store.ServiceClient.SetVector(BuildVector(value));
  }

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    ArgumentNullException.ThrowIfNull(value);
    value.Key = key;
    Add(value);
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

  public async Task AddAsync(VectorEntry<T> value, CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.SetVectorAsync(BuildVector(value), cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  public async Task AddAsync(VectorKey key, VectorEntry<T> value, CancellationToken cancellationToken = default)
  {
    value.Key = key;
    await AddAsync(value, cancellationToken);
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

  /// <summary>
  /// Stores one entry with both vectors in a single write, so the dense and the
  /// lexical slot always describe the same revision of the entry. Either vector
  /// may be null; both null is a content-only entry.
  /// </summary>
  public Task AddAsync(VectorKey key, T content, float[] embeddings, SparseVector sparse,
    CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embedding = embeddings, Sparse = sparse
    }, cancellationToken);
  }

  private Vector BuildVector(VectorEntry<T> value)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(value.Key.Value);
    ArgumentNullException.ThrowIfNull(value.Content);

    var vector = new Vector()
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(value.Key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(value.Content).Span),
    };

    if (value.Embedding != null)
      vector.Embeddings.AddRange(value.Embedding);

    if (value.Sparse != null)
      vector.Sparse = ToProtoSparseVector(value.Sparse);

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

  private static ProtoSparseVector ToProtoSparseVector(SparseVector sparse)
  {
    var message = new ProtoSparseVector();
    message.Terms.AddRange(sparse.Terms.ToArray());
    message.Weights.AddRange(sparse.Weights.ToArray());
    return message;
  }

  private static SparseVector FromProtoSparseVector(ProtoSparseVector sparse) =>
    sparse is null || sparse.Terms.Count == 0
      ? null
      : SparseVector.Create(sparse.Terms.ToArray(), sparse.Weights.ToArray());

  /// <summary>
  /// Bulk insert over a single client-streaming call: one round trip for the
  /// whole batch instead of one per entry. Entries carry their own embeddings
  /// (possibly none). Returns the number of entries accepted by the server.
  /// </summary>
  ///
  public async Task<int> AddRangeAsync(IEnumerable<KeyValuePair<VectorKey, VectorEntry<T>>> entries, CancellationToken cancellationToken = default)
  {
    foreach (var e in entries)
    {
      e.Value.Key = e.Key;
    }

    return await AddRangeAsync(entries.Select(i => i.Value), cancellationToken);
  }

  public async Task<int> AddRangeAsync(IEnumerable<VectorEntry<T>> entries, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entries);

    using var call = store.ServiceClient.SetVectors(cancellationToken: cancellationToken);

    foreach (var entry in entries)
      await call.RequestStream.WriteAsync(BuildVector(entry), cancellationToken).ConfigureAwait(false);

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
  public Task<int> AddRangeAsync(IEnumerable<(VectorKey Key, T Content, string Sentence)> entries,
    CancellationToken cancellationToken = default) =>
    AddRangeAsync(entries.Select(e => (e.Key, e.Content, e.Sentence, Sparse: (SparseVector)null)),
      cancellationToken);

  /// <summary>
  /// Bulk insert of documents with an optional lexical slot. The embedding is
  /// computed server-side from the sentence when one is given; the sparse vector
  /// is never derived from it.
  /// </summary>
  public async Task<int> AddRangeAsync(
    IEnumerable<(VectorKey Key, T Content, string Sentence, SparseVector Sparse)> entries,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entries);

    using var call = store.ServiceClient.SetDocuments(cancellationToken: cancellationToken);

    foreach (var (key, content, sentence, sparse) in entries)
    {
      ArgumentNullException.ThrowIfNull(key.Value);
      ArgumentNullException.ThrowIfNull(content);

      var document = new Document()
      {
        Database = GetDatabaseName(),
        Collection = GetCollectionName(),
        Key = ByteString.CopyFrom(key.Value),
        Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span),
        Sentence = sentence ?? string.Empty
      };

      if (sparse != null)
        document.Sparse = ToProtoSparseVector(sparse);

      await call.RequestStream.WriteAsync(document, cancellationToken).ConfigureAwait(false);
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

  public Task<List<VectorSearchResult<T>>> SearchAsync(float[] embeddings, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    return SearchAsync(embeddings, predicate: null, top: top, options: options, cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(float[] embeddings, Expression<Func<T, bool>> predicate, int top = 10,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var result = await store.ServiceClient
      .SearchVectorAsync(BuildSearchVectorRequest(embeddings, predicate, top, options), cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  public List<VectorSearchResult<T>> Search(string sentence, int top = 10, SearchOptions options = null)
  {
    return Search(sentence, predicate: null, top: top, options: options);
  }

  public Task<List<VectorSearchResult<T>>> SearchAsync(string sentence, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    return SearchAsync(sentence, predicate: null, top: top, options: options, cancellationToken: cancellationToken);
  }

  public List<VectorSearchResult<T>> Search(string sentence, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return MapResults(store.ServiceClient.SearchDocument(BuildSearchDocumentRequest(sentence, predicate, top, options)));
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(string sentence, Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    var result = await store.ServiceClient
      .SearchDocumentAsync(BuildSearchDocumentRequest(sentence, predicate, top, options), cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  // ── Lexical slot ──

  /// <summary>
  /// Reads the sparse vector stored with a key. Null when the key does not
  /// exist or the entry was written without a lexical slot.
  /// </summary>
  public SparseVector GetSparse(VectorKey key)
  {
    var result = store.ServiceClient.GetSparse(ToItemKey(key));
    return FromProtoSparseVector(result);
  }

  /// <summary>Async counterpart of <see cref="GetSparse"/>.</summary>
  public async Task<SparseVector> GetSparseAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetSparseAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return FromProtoSparseVector(result);
  }

  /// <summary>
  /// Exact lexical search: the entries sharing at least one term with the query,
  /// ranked by the sum of the shared weight products.
  /// </summary>
  public List<VectorSearchResult<T>> Search(SparseVector sparse, int top = 10, SearchOptions options = null)
  {
    return Search(sparse, predicate: null, top: top, options: options);
  }

  public List<VectorSearchResult<T>> Search(SparseVector sparse, Expression<Func<T, bool>> predicate, int top = 10,
    SearchOptions options = null)
  {
    ArgumentNullException.ThrowIfNull(sparse);
    return MapResults(store.ServiceClient.SearchSparse(BuildSearchSparseRequest(sparse, predicate, top, options)));
  }

  public Task<List<VectorSearchResult<T>>> SearchAsync(SparseVector sparse, int top = 10,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchAsync(sparse, predicate: null, top: top, options: options, cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(SparseVector sparse,
    Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(sparse);

    var result = await store.ServiceClient
      .SearchSparseAsync(BuildSearchSparseRequest(sparse, predicate, top, options), cancellationToken: cancellationToken)
      .ConfigureAwait(false);
    return MapResults(result);
  }

  /// <summary>
  /// Hybrid search: the dense and the lexical ranking are fused by position.
  /// At least one vector must be supplied; when both are, both weights are
  /// required, and a zero weight leaves that modality out.
  /// </summary>
  /// <summary>
  /// Hybrid search from a sentence: the server generates the dense embedding and
  /// the sparse lexical vector from the same text, then fuses them. No vector has
  /// to be computed on this side.
  /// </summary>
  public List<VectorSearchResult<T>> SearchHybrid(string sentence, int top = 10,
    float denseWeight = 1f, float sparseWeight = 1f, SearchOptions options = null)
  {
    return SearchHybrid(sentence, predicate: null, top: top,
      denseWeight: denseWeight, sparseWeight: sparseWeight, options: options);
  }

  public List<VectorSearchResult<T>> SearchHybrid(string sentence,
    Expression<Func<T, bool>> predicate, int top = 10, float denseWeight = 1f, float sparseWeight = 1f,
    SearchOptions options = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sentence);
    return MapResults(store.ServiceClient.SearchHybrid(
      BuildSearchHybridRequest(sentence, predicate, top, denseWeight, sparseWeight, options)));
  }

  public Task<List<VectorSearchResult<T>>> SearchHybridAsync(string sentence, int top = 10,
    float denseWeight = 1f, float sparseWeight = 1f, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    return SearchHybridAsync(sentence, predicate: null, top: top, denseWeight: denseWeight,
      sparseWeight: sparseWeight, options: options, cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchHybridAsync(string sentence,
    Expression<Func<T, bool>> predicate, int top = 10, float denseWeight = 1f, float sparseWeight = 1f,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sentence);

    var result = await store.ServiceClient
      .SearchHybridAsync(BuildSearchHybridRequest(sentence, predicate, top, denseWeight, sparseWeight, options),
        cancellationToken: cancellationToken)
      .ConfigureAwait(false);
    return MapResults(result);
  }

  // Named SearchHybrid, not an overload of Search: a null second argument would
  // otherwise be ambiguous between "no predicate" and "no sparse vector".
  public List<VectorSearchResult<T>> SearchHybrid(float[] embeddings, SparseVector sparse, int top = 10,
    float denseWeight = 1f, float sparseWeight = 1f, SearchOptions options = null)
  {
    return SearchHybrid(embeddings, sparse, predicate: null, top: top,
      denseWeight: denseWeight, sparseWeight: sparseWeight, options: options);
  }

  public List<VectorSearchResult<T>> SearchHybrid(float[] embeddings, SparseVector sparse,
    Expression<Func<T, bool>> predicate, int top = 10, float denseWeight = 1f, float sparseWeight = 1f,
    SearchOptions options = null)
  {
    return MapResults(store.ServiceClient.SearchHybrid(
      BuildSearchHybridRequest(embeddings, sparse, predicate, top, denseWeight, sparseWeight, options)));
  }

  public Task<List<VectorSearchResult<T>>> SearchHybridAsync(float[] embeddings, SparseVector sparse, int top = 10,
    float denseWeight = 1f, float sparseWeight = 1f, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    return SearchHybridAsync(embeddings, sparse, predicate: null, top: top, denseWeight: denseWeight,
      sparseWeight: sparseWeight, options: options, cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchHybridAsync(float[] embeddings, SparseVector sparse,
    Expression<Func<T, bool>> predicate, int top = 10, float denseWeight = 1f, float sparseWeight = 1f,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient
      .SearchHybridAsync(BuildSearchHybridRequest(embeddings, sparse, predicate, top, denseWeight, sparseWeight, options),
        cancellationToken: cancellationToken)
      .ConfigureAwait(false);
    return MapResults(result);
  }

  private SearchSparseRequest BuildSearchSparseRequest(SparseVector sparse,
    Expression<Func<T, bool>> predicate, int top, SearchOptions options)
  {
    var request = new SearchSparseRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Sparse = ToProtoSparseVector(sparse),
      Top = top,
      Tuning = BuildTuning(options)
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    return request;
  }

  private SearchHybridRequest BuildSearchHybridRequest(string sentence,
    Expression<Func<T, bool>> predicate, int top, float denseWeight, float sparseWeight, SearchOptions options)
  {
    var request = new SearchHybridRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Sentence = sentence,
      Top = top,
      DenseWeight = denseWeight,
      SparseWeight = sparseWeight,
      Tuning = BuildTuning(options)
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    return request;
  }

  private SearchHybridRequest BuildSearchHybridRequest(float[] embeddings, SparseVector sparse,
    Expression<Func<T, bool>> predicate, int top, float denseWeight, float sparseWeight, SearchOptions options)
  {
    var denseSupplied = embeddings is { Length: > 0 };
    if (!denseSupplied && sparse is null)
      throw new ArgumentException("A hybrid search needs at least one of the two vectors.");

    var request = new SearchHybridRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Top = top,
      Tuning = BuildTuning(options)
    };

    if (denseSupplied)
      request.Embeddings.AddRange(embeddings);

    if (sparse != null)
      request.Sparse = ToProtoSparseVector(sparse);

    // The server refuses both vectors without both weights: declare them here
    // so a caller that forgot one gets the error before the round trip.
    if (denseSupplied && sparse != null)
    {
      request.DenseWeight = denseWeight;
      request.SparseWeight = sparseWeight;
    }
    else if (denseSupplied)
    {
      request.DenseWeight = denseWeight;
    }
    else
    {
      request.SparseWeight = sparseWeight;
    }

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    return request;
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
      Score = i.Score,
      InDense = i.InDense,
      InSparse = i.InSparse,
      DenseScore = i.DenseScore,
      SparseScore = i.SparseScore
    }).ToList();
  }

  // ── Shared metadata: many embeddings reference one metadata record. ──

  /// <summary>
  /// Creates a shared metadata record and returns its opaque index. The index
  /// is stable across reopen and compaction and is never reused.
  /// </summary>
  public int CreateMetadata(T content)
  {
    ArgumentNullException.ThrowIfNull(content);
    return store.ServiceClient.CreateMetadata(new CreateMetadataRequest
    {
      Database = GetDatabaseName(),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span)
    }).MetadataIndex;
  }

  /// <summary>Async counterpart of <see cref="CreateMetadata"/>.</summary>
  public async Task<int> CreateMetadataAsync(T content, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(content);
    var result = await store.ServiceClient.CreateMetadataAsync(new CreateMetadataRequest
    {
      Database = GetDatabaseName(),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span)
    }, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.MetadataIndex;
  }

  /// <summary>
  /// Replaces the content of a shared metadata record. Every embedding
  /// associated to it reads the new content. False when the index is unknown.
  /// </summary>
  public bool UpdateMetadata(int metadataIndex, T content)
  {
    ArgumentNullException.ThrowIfNull(content);
    return store.ServiceClient.UpdateMetadata(new UpdateMetadataRequest
    {
      Database = GetDatabaseName(),
      MetadataIndex = metadataIndex,
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span)
    }).Updated;
  }

  /// <summary>Async counterpart of <see cref="UpdateMetadata"/>.</summary>
  public async Task<bool> UpdateMetadataAsync(int metadataIndex, T content, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(content);
    var result = await store.ServiceClient.UpdateMetadataAsync(new UpdateMetadataRequest
    {
      Database = GetDatabaseName(),
      MetadataIndex = metadataIndex,
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span)
    }, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Updated;
  }

  /// <summary>
  /// Returns the metadata index associated to a key, or -1 when the key is
  /// absent or not associated.
  /// </summary>
  public int GetMetadataIndex(VectorKey key)
  {
    return store.ServiceClient.GetEmbeddingMetadata(new EmbeddingMetadataRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value)
    }).MetadataIndex;
  }

  /// <summary>Async counterpart of <see cref="GetMetadataIndex"/>.</summary>
  public async Task<int> GetMetadataIndexAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetEmbeddingMetadataAsync(new EmbeddingMetadataRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value)
    }, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.MetadataIndex;
  }

  /// <summary>
  /// Associates an existing key to a shared metadata record, moving it when it
  /// was already associated to another record.
  /// </summary>
  public bool AssociateMetadata(VectorKey key, int metadataIndex)
  {
    return store.ServiceClient.AssociateEmbedding(new AssociateEmbeddingRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      MetadataIndex = metadataIndex
    }).Success;
  }

  /// <summary>Async counterpart of <see cref="AssociateMetadata"/>.</summary>
  public async Task<bool> AssociateMetadataAsync(VectorKey key, int metadataIndex, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.AssociateEmbeddingAsync(new AssociateEmbeddingRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      MetadataIndex = metadataIndex
    }, cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Success;
  }

  /// <summary>
  /// Grouped search: one result per shared metadata record, ranked by the
  /// highest observed embedding score. <paramref name="candidateBudget"/> is
  /// mandatory and caps the sum of requested candidates across attempts,
  /// repeated candidates included. Fewer than <paramref name="top"/> groups may
  /// be returned; exact grouped top-k is not guaranteed by approximate indexes.
  /// </summary>
  public List<MetadataSearchResult<T>> SearchByMetadata(float[] embeddings, int top, int candidateBudget, SearchOptions options = null)
  {
    return SearchByMetadata(embeddings, predicate: null, top, candidateBudget, options);
  }

  /// <summary>
  /// Grouped search restricted by a predicate. The predicate is translated
  /// client-side into the filter AST and evaluated server-side, exactly like
  /// <see cref="Search(float[], Expression{Func{T, bool}}, int, SearchOptions)"/>.
  /// </summary>
  public List<MetadataSearchResult<T>> SearchByMetadata(float[] embeddings, Expression<Func<T, bool>> predicate,
    int top, int candidateBudget, SearchOptions options = null)
  {
    if (embeddings == null || embeddings.Length == 0)
      throw new ArgumentException("Embeddings are required", nameof(embeddings));

    return MapMetadataResults(store.ServiceClient.SearchByMetadata(
      BuildSearchByMetadataRequest(embeddings, sentence: null, predicate, top, candidateBudget, options)));
  }

  /// <summary>Async counterpart of the embedding overload.</summary>
  public Task<List<MetadataSearchResult<T>>> SearchByMetadataAsync(float[] embeddings, int top, int candidateBudget,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchByMetadataAsync(embeddings, predicate: null, top, candidateBudget, options, cancellationToken);
  }

  /// <summary>Async counterpart of the filtered embedding overload.</summary>
  public async Task<List<MetadataSearchResult<T>>> SearchByMetadataAsync(float[] embeddings, Expression<Func<T, bool>> predicate,
    int top, int candidateBudget, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (embeddings == null || embeddings.Length == 0)
      throw new ArgumentException("Embeddings are required", nameof(embeddings));

    var result = await store.ServiceClient.SearchByMetadataAsync(
      BuildSearchByMetadataRequest(embeddings, sentence: null, predicate, top, candidateBudget, options),
      cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapMetadataResults(result);
  }

  /// <summary>
  /// Grouped search from a sentence: the server calculates the query vector.
  /// </summary>
  public List<MetadataSearchResult<T>> SearchByMetadata(string sentence, int top, int candidateBudget, SearchOptions options = null)
  {
    return SearchByMetadata(sentence, predicate: null, top, candidateBudget, options);
  }

  /// <summary>Filtered counterpart of the sentence overload.</summary>
  public List<MetadataSearchResult<T>> SearchByMetadata(string sentence, Expression<Func<T, bool>> predicate,
    int top, int candidateBudget, SearchOptions options = null)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      throw new ArgumentException("A sentence is required", nameof(sentence));

    return MapMetadataResults(store.ServiceClient.SearchByMetadata(
      BuildSearchByMetadataRequest(embeddings: null, sentence, predicate, top, candidateBudget, options)));
  }

  /// <summary>Async counterpart of the sentence overload.</summary>
  public Task<List<MetadataSearchResult<T>>> SearchByMetadataAsync(string sentence, int top, int candidateBudget,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchByMetadataAsync(sentence, predicate: null, top, candidateBudget, options, cancellationToken);
  }

  /// <summary>Async counterpart of the filtered sentence overload.</summary>
  public async Task<List<MetadataSearchResult<T>>> SearchByMetadataAsync(string sentence, Expression<Func<T, bool>> predicate,
    int top, int candidateBudget, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      throw new ArgumentException("A sentence is required", nameof(sentence));

    var result = await store.ServiceClient.SearchByMetadataAsync(
      BuildSearchByMetadataRequest(embeddings: null, sentence, predicate, top, candidateBudget, options),
      cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapMetadataResults(result);
  }

  private SearchByMetadataRequest BuildSearchByMetadataRequest(float[] embeddings, string sentence,
    Expression<Func<T, bool>> predicate, int top, int candidateBudget, SearchOptions options)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);

    var request = new SearchByMetadataRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Top = top,
      CandidateBudget = candidateBudget,
      Tuning = BuildTuning(options)
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    if (embeddings is { Length: > 0 })
      request.Embeddings.AddRange(embeddings);
    if (!string.IsNullOrWhiteSpace(sentence))
      request.Sentence = sentence;

    return request;
  }

  private List<MetadataSearchResult<T>> MapMetadataResults(SearchByMetadataResponse response)
  {
    return response.Results.Select(i => new MetadataSearchResult<T>
    {
      MetadataIndex = i.MetadataIndex,
      Key = i.Key.ToByteArray(),
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

  /// <summary>
  /// Reads the stored full-precision embedding of a key, or null when the key
  /// does not exist or the entry was stored without a vector. Useful for
  /// "more like this" searches: <c>Search(GetEmbedding(key), top)</c>.
  /// </summary>
  public float[] GetEmbedding(VectorKey key)
  {
    var result = store.ServiceClient.GetEmbedding(ToItemKey(key));
    return result.Embeddings.Count > 0 ? result.Embeddings.ToArray() : null;
  }

  /// <summary>
  /// Async counterpart of <see cref="GetEmbedding"/>.
  /// </summary>
  public async Task<float[]> GetEmbeddingAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetEmbeddingAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Embeddings.Count > 0 ? result.Embeddings.ToArray() : null;
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