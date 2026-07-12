using System.Collections;
using System.Linq.Expressions;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;

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

  public void Add(VectorKey key, T content, float[] embeddings)
  {
    Add(key, new VectorEntry<T> { Content = content, Embedding = embeddings });
  }

  /// <summary>
  /// Adds a document whose embedding is computed from the sentence via
  /// <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public void Add(VectorKey key, T content, string sentence)
  {
    Add(key, new VectorEntry<T> { Content = content, Embedding = EmbedSentence(sentence) });
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
      Embedding = value.Embedding
    });
  }

  public Task AddAsync(VectorKey key, T content, float[] embeddings)
  {
    return AddAsync(key, new VectorEntry<T> { Content = content, Embedding = embeddings });
  }

  /// <summary>
  /// Adds a document whose embedding is computed from the sentence via
  /// <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public Task AddAsync(VectorKey key, T content, string sentence)
  {
    return AddAsync(key, new VectorEntry<T> { Content = content, Embedding = EmbedSentence(sentence) });
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
  /// Reads the stored full-precision embedding of a key, or null when the key
  /// does not exist or was stored without a vector.
  /// </summary>
  public float[] GetEmbedding(VectorKey key)
  {
    return store.GetEmbedding(CollectionName, key.Value);
  }

  /// <summary>
  /// Nearest-neighbour search by query vector, with an optional predicate
  /// (translated to a serialized filter, evaluated without deserialization)
  /// and an optional per-query HNSW beam width (0 = index default).
  /// </summary>
  public List<VectorSearchResult<T>> Search(float[] embeddings, int top = 10, Expression<Func<T, bool>> predicate = null, int efSearch = 0)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var filter = predicate != null ? ExpressionTranslator.Translate(predicate) : null;

    return store.Search(CollectionName, embeddings, top, efSearch, filter)
      .Select(i => new VectorSearchResult<T>
      {
        Key = i.entry.Id,
        Content = i.entry.Content.Length > 0 ? _options.DocumentSerializer.Deserialize<T>(i.entry.Content) : null,
        Score = i.score
      })
      .ToList();
  }

  /// <summary>
  /// Nearest-neighbour search by sentence: the query embedding is computed via
  /// <see cref="VectorCollectionOptions{T}.SentenceEmbedder"/>.
  /// </summary>
  public List<VectorSearchResult<T>> Search(string sentence, int top = 10, Expression<Func<T, bool>> predicate = null, int efSearch = 0)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    return Search(EmbedSentence(sentence), top, predicate, efSearch);
  }

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
          Embedding = k.embeddings
        }).ToArray();
      }

      return Array.Empty<VectorEntry<T>>();
    }
  }
}
