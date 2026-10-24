using System.Collections;
using System.Linq.Expressions;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;
using Jigen.Index.Sparse;

namespace Jigen;

public class VectorCollection<T>(Store store, VectorCollectionOptions<T> options = null) :
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  private readonly VectorCollectionOptions<T> _options = options ?? new VectorCollectionOptions<T>();

  private string CollectionName => _options.Name;

  public IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> GetEnumerator()
  {
    if (!store.PositionIndex.TryGetValue(CollectionName, out var value)) yield break;

    foreach (var k in value.Keys)
    {
      var content = store.GetContent(CollectionName, k);
      if (content != null)
        yield return new KeyValuePair<VectorKey, VectorEntry<T>>(k,
          new VectorEntry<T>()
          {
            Key = k, Content = _options.DocumentSerializer.Deserialize<T>(content)
          });
    }
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
    ClearAsync().GetAwaiter().GetResult();
  }

  public Task ClearAsync()
  {
    return store.ClearContent(CollectionName);
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

  public int Count => store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    AddAsync(key, value).GetAwaiter().GetResult();
  }

  /// <summary>Adds a document with one vector, stored as a one-vector entry.</summary>
  public void Add(VectorKey key, T content, float[] embeddings)
  {
    Add(key, new VectorEntry<T> { Content = content, Embeddings = [embeddings] });
  }

  /// <summary>Adds a document stored with several vectors (e.g. one per passage).</summary>
  public void Add(VectorKey key, T content, IReadOnlyList<float[]> embeddings)
  {
    Add(key, new VectorEntry<T> { Content = content, Embeddings = embeddings });
  }

  /// <summary>
  /// Adds a document whose embedding is computed from the sentence via
  /// <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public void Add(VectorKey key, T content, string sentence)
  {
    Add(key, new VectorEntry<T> { Content = content, Embeddings = [EmbedSentence(sentence)] });
  }

  public Task AddAsync(VectorKey key, VectorEntry<T> value)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(value.Content);

    return store.AppendContent(new VectorEntry()
    {
      Id = key.Value,
      CollectionName = CollectionName,
      Content = _options.DocumentSerializer.Serialize(value.Content),
      Embeddings = value.Embeddings.Select(e => (ReadOnlyMemory<float>)e).ToArray(),
      SparseVectors = value.SparseVectors
    });
  }

  public Task AddAsync(VectorKey key, T content, float[] embeddings)
  {
    return AddAsync(key, new VectorEntry<T> { Content = content, Embeddings = [embeddings] });
  }

  /// <summary>Adds a document stored with several vectors (e.g. one per passage).</summary>
  public Task AddAsync(VectorKey key, T content, IReadOnlyList<float[]> embeddings)
  {
    return AddAsync(key, new VectorEntry<T> { Content = content, Embeddings = embeddings });
  }

  /// <summary>
  /// Adds a document whose embedding is computed from the sentence via
  /// <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public Task AddAsync(VectorKey key, T content, string sentence)
  {
    return AddAsync(key, new VectorEntry<T> { Content = content, Embeddings = [EmbedSentence(sentence)] });
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(key.Value);
  }

  public bool Remove(VectorKey key)
  {
    return RemoveAsync(key).GetAwaiter().GetResult();
  }

  public Task<bool> RemoveAsync(VectorKey key)
  {
    return store.DeleteContent(CollectionName, key.Value);
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    var result = store.GetContent(CollectionName, key.Value);
    value = result != null
      ? new VectorEntry<T>()
      {
        Key = key.Value,
        Content = _options.DocumentSerializer.Deserialize<T>(result)
      }
      : null;
    return result != null;
  }

  /// <summary>
  /// Reads every stored vector of a key, or null when the key does not exist or
  /// was stored without vectors.
  /// </summary>
  public float[][] GetEmbeddings(VectorKey key)
  {
    return store.GetEmbeddings(CollectionName, key.Value);
  }

  /// <summary>
  /// Reads every stored sparse vector of a key, each ordered by ascending term,
  /// or null when the key does not exist, has no vector record, or the archive
  /// predates sparse vectors.
  /// </summary>
  public IReadOnlyList<SparseTerm[]> GetSparseVectors(VectorKey key)
  {
    return store.GetSparseVectors(CollectionName, key.Value);
  }

  /// <summary>
  /// Best documents of this collection by sparse score, ordered by descending
  /// score then binary key. Returns an empty list while no document with sparse
  /// vectors has been indexed yet.
  /// </summary>
  public IReadOnlyList<SparseSearchResult> SearchSparse(IReadOnlyDictionary<int, float> query, int top = 10,
    CancellationToken cancellationToken = default)
  {
    return store.SearchSparse(CollectionName, query, top, cancellationToken);
  }

  /// <summary>
  /// Dense nearest-neighbour search by query vector, with an optional predicate
  /// (translated to a serialized filter, evaluated without deserialization)
  /// and an optional per-query HNSW beam width (0 = index default).
  /// </summary>
  public List<VectorSearchResult<T>> SearchDense(float[] embeddings, int top = 10, Expression<Func<T, bool>> predicate = null, int efSearch = 0)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var filter = predicate != null ? ExpressionTranslator.Translate(predicate) : null;

    return store.SearchDense(CollectionName, embeddings, top, efSearch, filter)
      .Select(i => new VectorSearchResult<T>
      {
        Key = i.entry.Id,
        Content = i.entry.Content.Length > 0 ? _options.DocumentSerializer.Deserialize<T>(i.entry.Content) : null,
        Score = i.score,
        DenseScore = i.score
      })
      .ToList();
  }

  /// <summary>
  /// Dense nearest-neighbour search by sentence: the query embedding is computed
  /// via <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public List<VectorSearchResult<T>> SearchDense(string sentence, int top = 10, Expression<Func<T, bool>> predicate = null, int efSearch = 0)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return SearchDense(EmbedSentence(sentence), top, predicate, efSearch);
  }

  /// <summary>
  /// Hybrid search over both halves of a document. Either query may be omitted;
  /// supplying neither is an error.
  /// <para>
  /// The two scores live on different scales (cosine in [-1, 1] for the dense
  /// half, an unbounded inner product for the sparse one), so they are fused by
  /// rank: a document scores 1 / (60 + position) in each list it appears in, and
  /// the values are summed. <see cref="VectorSearchResult{T}.Score"/> holds that
  /// fused value; the partial scores stay available on the result.
  /// </para>
  /// <para>
  /// The predicate filters the dense half only: it is evaluated on stored content
  /// without reading it, which the sparse half cannot do.
  /// </para>
  /// </summary>
  public List<VectorSearchResult<T>> Search(float[] embeddings = null,
    IReadOnlyDictionary<int, float> sparseQuery = null, int top = 10,
    Expression<Func<T, bool>> predicate = null, int efSearch = 0,
    CancellationToken cancellationToken = default)
  {
    if (embeddings == null && sparseQuery == null)
      throw new ArgumentException("A hybrid search needs a dense query, a sparse query, or both.");
    if (top <= 0)
      return [];

    var fused = new Dictionary<string, (VectorKey Key, T Content, double Score, float? Dense, float? Sparse)>(
      StringComparer.Ordinal);

    void Add(byte[] key, T content, double contribution, float? dense, float? sparse)
    {
      var identity = Convert.ToHexString(key);
      if (fused.TryGetValue(identity, out var existing))
        fused[identity] = (existing.Key, existing.Content ?? content, existing.Score + contribution,
          dense ?? existing.Dense, sparse ?? existing.Sparse);
      else
        fused[identity] = (new VectorKey { Value = key }, content, contribution, dense, sparse);
    }

    if (embeddings != null)
    {
      var denseHits = SearchDense(embeddings, top, predicate, efSearch);
      for (var rank = 0; rank < denseHits.Count; rank++)
        Add(denseHits[rank].Key.Value, denseHits[rank].Content,
          1d / (ReciprocalRankOffset + rank + 1), denseHits[rank].Score, null);
    }

    if (sparseQuery != null)
    {
      var sparseHits = store.SearchSparse(CollectionName, sparseQuery, top, cancellationToken);
      for (var rank = 0; rank < sparseHits.Count; rank++)
      {
        var key = sparseHits[rank].DocumentId.ToArray();
        var content = fused.TryGetValue(Convert.ToHexString(key), out var known)
          ? known.Content
          : TryGetValue(new VectorKey { Value = key }, out var entry) ? entry.Content : null;
        Add(key, content, 1d / (ReciprocalRankOffset + rank + 1), null, (float)sparseHits[rank].Score);
      }
    }

    return fused.Values
      .OrderByDescending(hit => hit.Score)
      .ThenBy(hit => hit.Key.Value, ByteArrayOrder.Instance)
      .Take(top)
      .Select(hit => new VectorSearchResult<T>
      {
        Key = hit.Key,
        Content = hit.Content,
        Score = (float)hit.Score,
        DenseScore = hit.Dense,
        SparseScore = hit.Sparse
      })
      .ToList();
  }

  // Standard reciprocal-rank constant: damps the influence of the first positions
  // without requiring a normalization of the two scores.
  private const int ReciprocalRankOffset = 60;

  private float[] EmbedSentence(string sentence)
  {
    ArgumentNullException.ThrowIfNull(sentence);

    if (_options.SentenceEmbedder is null)
      throw new InvalidOperationException(
        "The sentence-based overloads need VectorCollectionOptions.SentenceEmbedder " +
        "(e.g. generator.GenerateEmbedding from an OnnxEmbeddingGenerator).");

    return _options.SentenceEmbedder(sentence);
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => this.TryGetValue(key, out var result) ? result : null;
    set => this.Add(key, value);
  }

  public ICollection<VectorKey> Keys =>
    (store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();

  public ICollection<VectorEntry<T>> Values
  {
    get
    {
      if (store.PositionIndex.TryGetValue(CollectionName, out var value))
      {
        return value.Keys.Select(k => new
        {
          k,
          content = store.GetContent(CollectionName, k),
          embeddings = Array.Empty<float>()
        }).Select(k => new VectorEntry<T>()
        {
          Key = k.k,
          Content = k.content is { Length: > 0 } ? _options.DocumentSerializer.Deserialize<T>(k.content) : null,
          Embeddings = k.embeddings.Length > 0 ? [k.embeddings] : []
        }).ToArray();
      }

      return Array.Empty<VectorEntry<T>>();
    }
  }
}
