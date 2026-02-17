using System.Collections;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public class DocumentCollection<T>(Store store, DocumentCollectionOptions<T> options = null) :
  IDictionary<VectorKey, T>
  where T : class, new()
{
  private string CollectionName = options?.Name ?? nameof(T);

  public IEnumerator<KeyValuePair<VectorKey, T>> GetEnumerator()
  {
    if (!store.PositionIndex.TryGetValue(CollectionName, out var value)) yield break;

    foreach (var k in value.Keys)
    {
      var content = store.GetContent(CollectionName, k);
      if (content != null)
        yield return new KeyValuePair<VectorKey, T>(k, options.DocumentSerializer.Deserialize<T>(content));
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, T> item)
  {
    store.SetContent(new VectorEntry()
    {
      Id = item.Key.Value,
      CollectionName = CollectionName,
      Content = options.DocumentSerializer.Serialize(item.Value)
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

  public bool Contains(KeyValuePair<VectorKey, T> item)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(item.Key.Value);
  }

  public void CopyTo(KeyValuePair<VectorKey, T>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, T> item)
  {
    var result = store.PositionIndex.TryGetValue(CollectionName, out var index) &&
                 index.Remove(item.Key.Value);

    if (result) store.SaveIndexChanges().GetAwaiter().GetResult();
    return result;
  }

  public int Count => store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, T value)
  {
    store.SetContent(new VectorEntry()
    {
      Id = key.Value,
      CollectionName = CollectionName,
      Content = options.DocumentSerializer.Serialize(value)
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

    if (result) store.SaveIndexChanges().GetAwaiter().GetResult();
    return result;
  }

  public bool TryGetValue(VectorKey key, out T value)
  {
    var result = store.GetContent(CollectionName, key.Value);
    value = result != null ? options.DocumentSerializer.Deserialize<T>(result) : null;
    return result != null;
  }

  public T this[VectorKey key]
  {
    get => this.TryGetValue(key, out var result) ? result : null;
    set => this.Add(key, value);
  }

  public ICollection<VectorKey> Keys => (store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();

  public ICollection<T> Values
  {
    get
    {
      if (store.PositionIndex.TryGetValue(CollectionName, out var value))
      {
        return value.Keys.Select(k => new { k, content = store.GetContent(CollectionName, k) })
          .Select(k => k.content is { Length: > 0 } ? options.DocumentSerializer.Deserialize<T>(k.content) : null).ToArray();
      }

      return Array.Empty<T>();
    }
  }
}