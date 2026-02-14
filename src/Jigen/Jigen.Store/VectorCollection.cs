using System.Collections;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public class VectorCollection<T>(Store store, VectorCollectionOptions<T> options = null) :
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  private string CollectionName = options?.Name ?? nameof(T);
  private IDocumentSerializer<T> Serializer => store.GetSerializer<T>();

  public IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> GetEnumerator()
  {
    if (!store.PositionIndex.TryGetValue(CollectionName, out var value)) yield break;

    foreach (var k in value.Keys)
    {
      var content = store.GetContent(CollectionName, k);
      var embeddings = Array.Empty<float>(); //TODO: Aggiungere metodo per la lettura degli embeddings
      if (content != null) ;
      yield return new KeyValuePair<VectorKey, VectorEntry<T>>(k,
        new VectorEntry<T>()
        {
          Key = k, Content = Serializer.Deserialize(content), Embedding = embeddings
        });
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    store.IngestionQueue.Enqueue(new VectorEntry()
    {
      Id = item.Key.Key,
      CollectionName = CollectionName,
      Content = Serializer.Serialize(item.Value.Content),
      Embedding = item.Value.Embedding
    });
  }

  public void Clear()
  {
    if (store.PositionIndex.TryGetValue(CollectionName, out var index))
    {
      index.Clear();
      store.SaveIndexChanges();
    }
  }

  public bool Contains(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(item.Key.Key);
  }

  public void CopyTo(KeyValuePair<VectorKey, VectorEntry<T>>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    var result = store.PositionIndex.TryGetValue(CollectionName, out var index) &&
                 index.Remove(item.Key.Key);

    if (result) store.SaveIndexChanges().GetAwaiter().GetResult();
    return result;
  }

  public int Count => store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    store.IngestionQueue.Enqueue(new VectorEntry()
    {
      Id = key.Key,
      CollectionName = CollectionName,
      Content = Serializer.Serialize(value.Content),
      Embedding = value.Embedding
    });
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(key.Key);
  }

  public bool Remove(VectorKey key)
  {
    var result = store.PositionIndex.TryGetValue(CollectionName, out var index) &&
                 index.Remove(key.Key);

    if (result) store.SaveIndexChanges();
    return result;
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    var result = store.GetContent(CollectionName, key.Key);
    value = result != null
      ? new VectorEntry<T>()
      {
        Key = key.Key,
        Content = Serializer.Deserialize(result)
      }
      : null;
    return result != null;
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => this.TryGetValue(key, out var result) ? result : null;
    set => this.Add(key, value);
  }

  public ICollection<VectorKey> Keys => (store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();

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
          Content = k.content is { Length: > 0 } ? Serializer.Deserialize(k.content) : null,
          Embedding = k.embeddings
        }).ToArray();
      }
      return Array.Empty<VectorEntry<T>>();
    }
  }
}