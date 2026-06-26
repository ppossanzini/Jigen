using System.Collections;
using System.Linq.Expressions;
using Google.Protobuf;
using Google.Protobuf.Collections;
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

  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(value.Content);

    var v = new Vector()
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(value.Content).Span),
    };

    if (value.Embedding != null)
      v.Embeddings.AddRange(value.Embedding);

    store.ServiceClient.SetVector(v);
  }

  public void Add(VectorKey key, T content, string sentence)
  {
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(sentence);
    ArgumentNullException.ThrowIfNull(content);

    store.ServiceClient.SetDocument(new Document()
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Key = ByteString.CopyFrom(key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(content).Span),
      Sentence = sentence
    });
  }

  public void Add(VectorKey key, T content, float[] embeddings)
  {
    this.Add(key, new VectorEntry<T>()
    {
      Content = content, Embedding = embeddings
    });
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.ServiceClient.Contains(ToItemKey(key)).Success;
  }

  public List<VectorSearchResult<T>> Search(float[] embeddings, int top = 10)
  {
    if (embeddings == null || embeddings.Length == 0)
      return [];

    var request = new SearchVectorRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Top = top
    };

    request.Embeddings.AddRange(embeddings);

    var result = store.ServiceClient.SearchVector(request);
    return result.Results.Select(i => new VectorSearchResult<T>
    {
      Key = i.Key.ToByteArray(),
      Content = _options.DocumentSerializer.Deserialize<T>(i.Content.Memory),
      Score = i.Score
    }).ToList();
  }

  public List<VectorSearchResult<T>> Search(string sentence, int top = 10)
  {
    return Search(sentence, predicate: null, top: top);
  }

  public List<VectorSearchResult<T>> Search(string sentence, Expression<Func<T, bool>> predicate, int top = 10)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return [];

    var request = new SearchDocumentRequest
    {
      Database = GetDatabaseName(),
      Collection = GetCollectionName(),
      Sentence = sentence,
      Top = top
    };

    if (predicate != null)
      request.Filter = ProtoExpressionTranslator.Translate(predicate);

    var result = store.ServiceClient.SearchDocument(request);
    return result.Results.Select(i => new VectorSearchResult<T>
    {
      Key = i.Key.ToByteArray(),
      Content = _options.DocumentSerializer.Deserialize<T>(i.Content.Memory),
      Score = i.Score
    }).ToList();
  }

  public bool Remove(VectorKey key)
  {
    return store.ServiceClient.DeleteVector(ToItemKey(key)).Success;
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    value = null;
    var result = store.ServiceClient.GetContent(ToItemKey(key));

    if (!result.Content.IsEmpty)
      value = new VectorEntry<T>()
      {
        Key = key,
        Content = _options.DocumentSerializer.Deserialize<T>(result.Content.Memory)
      };

    return !result.Content.IsEmpty;
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => TryGetValue(key, out var result) ? result : null;
    set => Add(key, value);
  }

  public ICollection<VectorKey> Keys => store.ServiceClient.GetAllKeys(CollectionKey).Keys.Select(k => (VectorKey)k.Span).ToList();

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