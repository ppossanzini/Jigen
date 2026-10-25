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

  /// <summary>Adds a document with one vector, stored as a one-vector entry.</summary>
  public void Add(VectorKey key, T content, float[] embeddings)
  {
    this.Add(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = [embeddings]
    });
  }

  /// <summary>Adds a document stored with several vectors (e.g. one per passage).</summary>
  public void Add(VectorKey key, T content, IReadOnlyList<float[]> embeddings)
  {
    Add(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = embeddings
    });
  }

  /// <summary>Adds a one-vector entry together with its sparse (lexical) terms.</summary>
  public void Add(VectorKey key, T content, float[] embeddings, IReadOnlyDictionary<int, float> sparse)
  {
    Add(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = [embeddings], SparseVectors = [sparse]
    });
  }

  /// <summary>Adds a multi-vector entry with one sparse list per vector, aligned by index.</summary>
  public void Add(VectorKey key, T content, IReadOnlyList<float[]> embeddings,
    IReadOnlyList<IReadOnlyDictionary<int, float>> sparse)
  {
    Add(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = embeddings, SparseVectors = sparse
    });
  }

  /// <summary>Adds a document whose embedding the server computes, with its sparse terms.</summary>
  public void Add(VectorKey key, T content, string sentence, IReadOnlyDictionary<int, float> sparse)
  {
    store.ServiceClient.SetDocument(BuildDocument(key, content, sentence, sparse));
  }

  /// <summary>
  /// Adds a document and lets the server fill both slots from the sentence: the
  /// dense embedding and, with <paramref name="sparseFromSentence"/>, the sparse
  /// terms, from the same tokenizer pass.
  /// </summary>
  public void Add(VectorKey key, T content, string sentence, bool sparseFromSentence)
  {
    store.ServiceClient.SetDocument(BuildDocument(key, content, sentence, null, sparseFromSentence));
  }

  public async Task AddAsync(VectorEntry<T> value, CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.SetVectorAsync(BuildVector(value), cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  public async Task AddAsync(VectorKey key, VectorEntry<T> value, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(value);
    value.Key = key;
    await AddAsync(value, cancellationToken);
  }

  public async Task AddAsync(VectorKey key, T content, string sentence, CancellationToken cancellationToken = default)
  {
    await store.ServiceClient.SetDocumentAsync(BuildDocument(key, content, sentence), cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Adds a document with one vector, stored as a one-vector entry.</summary>
  public Task AddAsync(VectorKey key, T content, float[] embeddings, CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = [embeddings]
    }, cancellationToken);
  }

  /// <summary>Adds a document stored with several vectors (e.g. one per passage).</summary>
  public Task AddAsync(VectorKey key, T content, IReadOnlyList<float[]> embeddings, CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = embeddings
    }, cancellationToken);
  }

  /// <summary>Adds a one-vector entry together with its sparse (lexical) terms.</summary>
  public Task AddAsync(VectorKey key, T content, float[] embeddings, IReadOnlyDictionary<int, float> sparse,
    CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = [embeddings], SparseVectors = [sparse]
    }, cancellationToken);
  }

  /// <summary>Adds a multi-vector entry with one sparse list per vector, aligned by index.</summary>
  public Task AddAsync(VectorKey key, T content, IReadOnlyList<float[]> embeddings,
    IReadOnlyList<IReadOnlyDictionary<int, float>> sparse, CancellationToken cancellationToken = default)
  {
    return AddAsync(key, new VectorEntry<T>()
    {
      Content = content, Embeddings = embeddings, SparseVectors = sparse
    }, cancellationToken);
  }

  /// <summary>Adds a document whose embedding the server computes, with its sparse terms.</summary>
  public async Task AddAsync(VectorKey key, T content, string sentence, IReadOnlyDictionary<int, float> sparse,
    CancellationToken cancellationToken = default)
  {
    await store.ServiceClient
      .SetDocumentAsync(BuildDocument(key, content, sentence, sparse), cancellationToken: cancellationToken)
      .ConfigureAwait(false);
  }

  /// <summary>
  /// Adds a document and lets the server fill both slots from the sentence: the
  /// dense embedding and, with <paramref name="sparseFromSentence"/>, the sparse
  /// terms, from the same tokenizer pass.
  /// </summary>
  public async Task AddAsync(VectorKey key, T content, string sentence, bool sparseFromSentence,
    CancellationToken cancellationToken = default)
  {
    await store.ServiceClient
      .SetDocumentAsync(BuildDocument(key, content, sentence, null, sparseFromSentence),
        cancellationToken: cancellationToken)
      .ConfigureAwait(false);
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

    foreach (var embeddings in value.Embeddings)
      vector.Vectors.Add(new EmbeddingVector { Embeddings = { embeddings } });

    if (value.SparseVectors.Count > value.Embeddings.Count)
      throw new ArgumentException("SparseVectors has more entries than Embeddings; they are aligned by index.",
        nameof(value));

    foreach (var sparse in value.SparseVectors)
    {
      var terms = new SparseTerms();
      foreach (var (term, weight) in sparse)
      {
        if (term < 0)
          throw new ArgumentException("Sparse term ids cannot be negative.", nameof(value));
        terms.Terms[term] = weight;
      }

      vector.Sparse.Add(terms);
    }

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

  private Document BuildDocument(VectorKey key, T content, string sentence, IReadOnlyDictionary<int, float> sparse,
    bool sparseFromSentence = false)
  {
    var document = BuildDocument(key, content, sentence);
    if (sparseFromSentence)
      document.SparseFromSentence = true;
    if (sparse is { Count: > 0 })
    {
      var terms = new SparseTerms();
      foreach (var (term, weight) in sparse)
      {
        if (term < 0)
          throw new ArgumentException("Sparse term ids cannot be negative.", nameof(sparse));
        terms.Terms[term] = weight;
      }

      document.Sparse = terms;
    }

    return document;
  }

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
    AddRangeAsync(entries, sparseFromSentence: false, cancellationToken);

  /// <summary>
  /// Same batch, letting the server fill both slots of every entry from its
  /// sentence: the dense embedding and, with <paramref name="sparseFromSentence"/>,
  /// the sparse terms, from the same tokenizer pass.
  /// </summary>
  public async Task<int> AddRangeAsync(IEnumerable<(VectorKey Key, T Content, string Sentence)> entries,
    bool sparseFromSentence, CancellationToken cancellationToken = default)
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
        Sentence = sentence ?? string.Empty,
        SparseFromSentence = sparseFromSentence
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

  /// <summary>Dense half only, by precomputed query vector.</summary>
  public List<VectorSearchResult<T>> SearchDense(float[] embeddings, int top = 10, SearchOptions options = null)
  {
    return SearchDense(embeddings, predicate: null, top: top, options: options);
  }

  public List<VectorSearchResult<T>> SearchDense(float[] embeddings, Expression<Func<T, bool>> predicate, int top = 10,
    SearchOptions options = null)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    return MapResults(store.ServiceClient.SearchVector(BuildSearchVectorRequest(embeddings, predicate, top, options)));
  }

  public Task<List<VectorSearchResult<T>>> SearchDenseAsync(float[] embeddings, int top = 10,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchDenseAsync(embeddings, predicate: null, top: top, options: options,
      cancellationToken: cancellationToken);
  }

  public async Task<List<VectorSearchResult<T>>> SearchDenseAsync(float[] embeddings,
    Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var result = await store.ServiceClient
      .SearchVectorAsync(BuildSearchVectorRequest(embeddings, predicate, top, options),
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  /// <summary>Dense half only, by sentence: the server computes the embedding.</summary>
  public List<VectorSearchResult<T>> SearchDense(string sentence, int top = 10, SearchOptions options = null)
  {
    return SearchDense(sentence, predicate: null, top: top, options: options);
  }

  public Task<List<VectorSearchResult<T>>> SearchDenseAsync(string sentence, int top = 10,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    return SearchDenseAsync(sentence, predicate: null, top: top, options: options,
      cancellationToken: cancellationToken);
  }

  public List<VectorSearchResult<T>> SearchDense(string sentence, Expression<Func<T, bool>> predicate, int top = 10,
    SearchOptions options = null)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return MapResults(store.ServiceClient.SearchDocument(
      BuildSearchDocumentRequest(sentence, predicate, top, options)));
  }

  public async Task<List<VectorSearchResult<T>>> SearchDenseAsync(string sentence,
    Expression<Func<T, bool>> predicate, int top = 10, SearchOptions options = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    var result = await store.ServiceClient
      .SearchDocumentAsync(BuildSearchDocumentRequest(sentence, predicate, top, options),
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  private SearchVectorRequest BuildSearchVectorRequest(float[] embeddings, Expression<Func<T, bool>> predicate, int top,
    SearchOptions options, IReadOnlyDictionary<int, float> sparseQuery = null)
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

    if (embeddings is { Length: > 0 })
      request.Embeddings.AddRange(embeddings);
    if (sparseQuery is not null)
      foreach (var (term, weight) in sparseQuery)
        request.SparseQuery[term] = weight;

    return request;
  }

  /// <summary>
  /// Sparse half only: the query maps term identifiers to weights.
  /// </summary>
  public List<VectorSearchResult<T>> SearchSparse(IReadOnlyDictionary<int, float> sparseQuery, int top = 10,
    SearchOptions options = null)
  {
    if (sparseQuery is null || sparseQuery.Count == 0)
      return [];

    return MapResults(store.ServiceClient.SearchVector(
      BuildSearchVectorRequest(null, predicate: null, top, options, sparseQuery)));
  }

  public async Task<List<VectorSearchResult<T>>> SearchSparseAsync(IReadOnlyDictionary<int, float> sparseQuery,
    int top = 10, SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (sparseQuery is null || sparseQuery.Count == 0)
      return [];

    var result = await store.ServiceClient
      .SearchVectorAsync(BuildSearchVectorRequest(null, predicate: null, top, options, sparseQuery),
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  /// <summary>
  /// Hybrid search: the dense and the sparse half fused by reciprocal rank.
  /// Either half may be omitted, but not both.
  /// </summary>
  public List<VectorSearchResult<T>> Search(float[] embeddings = null,
    IReadOnlyDictionary<int, float> sparseQuery = null, Expression<Func<T, bool>> predicate = null, int top = 10,
    SearchOptions options = null)
  {
    if (embeddings is not { Length: > 0 } && sparseQuery is null)
      throw new ArgumentException("A hybrid search needs a dense query, a sparse query, or both.");

    return MapResults(store.ServiceClient.SearchVector(
      BuildSearchVectorRequest(embeddings, predicate, top, options, sparseQuery)));
  }

  public async Task<List<VectorSearchResult<T>>> SearchAsync(float[] embeddings,
    IReadOnlyDictionary<int, float> sparseQuery, Expression<Func<T, bool>> predicate = null, int top = 10,
    SearchOptions options = null, CancellationToken cancellationToken = default)
  {
    if (embeddings is not { Length: > 0 } && sparseQuery is null)
      throw new ArgumentException("A hybrid search needs a dense query, a sparse query, or both.");

    var result = await store.ServiceClient
      .SearchVectorAsync(BuildSearchVectorRequest(embeddings, predicate, top, options, sparseQuery),
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  /// <summary>
  /// Hybrid search from a sentence. With <paramref name="sparseFromSentence"/>
  /// the server derives the sparse half from the same text it embeds densely, so
  /// a hybrid search needs no sparse query at all; without it the sentence fills
  /// only the dense half, unless <paramref name="sparseQuery"/> is supplied.
  /// </summary>
  public List<VectorSearchResult<T>> Search(string sentence,
    IReadOnlyDictionary<int, float> sparseQuery = null, Expression<Func<T, bool>> predicate = null, int top = 10,
    SearchOptions options = null, bool sparseFromSentence = false)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return MapResults(store.ServiceClient.SearchDocument(
      BuildSearchDocumentRequest(sentence, predicate, top, options, sparseQuery, sparseFromSentence)));
  }

  /// <summary>Hybrid search from a sentence with a predicate and no explicit sparse query.</summary>
  public List<VectorSearchResult<T>> Search(string sentence, Expression<Func<T, bool>> predicate, int top = 10,
    SearchOptions options = null, bool sparseFromSentence = false) =>
    Search(sentence, sparseQuery: null, predicate, top, options, sparseFromSentence);

  public Task<List<VectorSearchResult<T>>> SearchAsync(string sentence, Expression<Func<T, bool>> predicate,
    int top = 10, SearchOptions options = null, bool sparseFromSentence = false,
    CancellationToken cancellationToken = default) =>
    SearchAsync(sentence, sparseQuery: null, predicate, top, options, sparseFromSentence, cancellationToken);

  public async Task<List<VectorSearchResult<T>>> SearchAsync(string sentence,
    IReadOnlyDictionary<int, float> sparseQuery, Expression<Func<T, bool>> predicate = null, int top = 10,
    SearchOptions options = null, bool sparseFromSentence = false,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    var result = await store.ServiceClient
      .SearchDocumentAsync(BuildSearchDocumentRequest(sentence, predicate, top, options, sparseQuery, sparseFromSentence),
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return MapResults(result);
  }

  private SearchDocumentRequest BuildSearchDocumentRequest(string sentence, Expression<Func<T, bool>> predicate,
    int top, SearchOptions options, IReadOnlyDictionary<int, float> sparseQuery = null,
    bool sparseFromSentence = false)
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

    if (sparseQuery is not null)
      foreach (var (term, weight) in sparseQuery)
        request.SparseQuery[term] = weight;
    if (sparseFromSentence)
      request.SparseFromSentence = true;

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
    if (options.MinDenseScore.HasValue)
      tuning.MinDenseScore = options.MinDenseScore.Value;
    if (options.MinSparseScore.HasValue)
      tuning.MinSparseScore = options.MinSparseScore.Value;

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
      DenseScore = i.HasDenseScore ? i.DenseScore : null,
      SparseScore = i.HasSparseScore ? i.SparseScore : null
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
  /// Reads every stored vector of a key, or null when the key does not exist or
  /// was stored without vectors. Useful for "more like this" searches.
  /// </summary>
  public float[][] GetEmbeddings(VectorKey key)
  {
    var result = store.ServiceClient.GetEmbeddings(ToItemKey(key));
    return result.Vectors.Count > 0
      ? result.Vectors.Select(v => v.Embeddings.ToArray()).ToArray()
      : null;
  }

  /// <summary>Async counterpart of <see cref="GetEmbeddings"/>.</summary>
  public async Task<float[][]> GetEmbeddingsAsync(VectorKey key, CancellationToken cancellationToken = default)
  {
    var result = await store.ServiceClient.GetEmbeddingsAsync(ToItemKey(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    return result.Vectors.Count > 0
      ? result.Vectors.Select(v => v.Embeddings.ToArray()).ToArray()
      : null;
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