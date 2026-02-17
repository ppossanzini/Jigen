using System.Collections;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public class VectorCollection<T>(Store store, VectorCollectionOptions<T> options = null) :
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  private string CollectionName = options?.Name ?? nameof(T);

  public IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> GetEnumerator()
  {
    if (!store.PositionIndex.TryGetValue(CollectionName, out var value)) yield break;

    foreach (var k in value.Keys)
    {
      var content = store.GetContent(CollectionName, k);
      var embeddings = Array.Empty<float>(); //TODO: Aggiungere metodo per la lettura degli embeddings
      if (content != null)
        yield return new KeyValuePair<VectorKey, VectorEntry<T>>(k,
          new VectorEntry<T>()
          {
            Key = k, Content = options.DocumentSerializer.Deserialize<T>(content), Embedding = embeddings
          });
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    store.AppendContent(new VectorEntry()
    {
      Id = item.Key.Value,
      CollectionName = CollectionName,
      Content = options.DocumentSerializer.Serialize(item.Value.Content),
      Embedding = item.Value.Embedding
    }).GetAwaiter().GetResult();
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
           index.ContainsKey(item.Key.Value);
  }

  public void CopyTo(KeyValuePair<VectorKey, VectorEntry<T>>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    var result = store.PositionIndex.TryGetValue(CollectionName, out var index) &&
                 index.Remove(item.Key.Value);

    if (result) store.SaveIndexChanges().GetAwaiter().GetResult();
    return result;
  }

  public int Count => store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    store.AppendContent(new VectorEntry()
    {
      Id = key.Value,
      CollectionName = CollectionName,
      Content = options.DocumentSerializer.Serialize(value.Content),
      Embedding = value.Embedding
    }).GetAwaiter().GetResult();
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(key.Value);
  }

  public bool Remove(VectorKey key)
  {
    var result = store.PositionIndex.TryGetValue(CollectionName, out var index) &&
                 index.Remove(key.Value);

    if (result) store.SaveIndexChanges();
    return result;
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    var result = store.GetContent(CollectionName, key.Value);
    value = result != null
      ? new VectorEntry<T>()
      {
        Key = key.Value,
        Content = options.DocumentSerializer.Deserialize<T>(result)
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
          Content = k.content is { Length: > 0 } ? options.DocumentSerializer.Deserialize<T>(k.content) : null,
          Embedding = k.embeddings
        }).ToArray();
      }

      return Array.Empty<VectorEntry<T>>();
    }
  }
}