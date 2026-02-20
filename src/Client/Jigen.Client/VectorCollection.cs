using System.Collections;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Jigen.Client.BaseTypes;
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
    var v = new Vector()
    {
      Database = store.Options.DatabaseName,
      Collection = _options.Name,
      Key = ByteString.CopyFrom(key.Value),
      Content = ByteString.CopyFrom(_options.DocumentSerializer.Serialize(value.Content).Span),
    };

    v.Embeddings.AddRange(value.Embedding);
    store.ServiceClient.SetVector(v);
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.ServiceClient.Contains(ToItemKey(key)).Success;
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
  public ICollection<VectorEntry<T>> Values => store.ServiceClient.GetAllKeys(CollectionKey).Keys.Select(k => TryGetValue(k.Span, out var value) ? value : null).ToList();


  private ItemKey ToItemKey(VectorKey key) => new()
  {
    Database = store.Options.DatabaseName,
    Collection = _options.Name,
    Key = ByteString.CopyFrom(key.Value)
  };

  private CollectionKey CollectionKey => new()
  {
    Database = store.Options.DatabaseName, Collection = _options.Name
  };
}