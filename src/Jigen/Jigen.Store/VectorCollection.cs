using System.Collections;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public class VectorCollection<T> :
  ICollection<VectorEntry<T>>,
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  private readonly Store _store;
  private readonly int _dimensions;
  private readonly string _name;

  private readonly VectorCollectionOptions<T> options;

  public VectorCollection(Store store, VectorCollectionOptions<T> options)
  {
    if (string.IsNullOrEmpty(options.Name)) throw new ArgumentException("Collection name cannot be null or empty", nameof(options.Name));
    if (options.Name.Length > 256) throw new ArgumentException("Collection name cannot be longer than 256 characters", nameof(options.Name));

    this.options = options;

    this._store = store ?? throw new ArgumentNullException(nameof(store));
    this._dimensions = options.Dimensions;
    this._name = options.Name;

    store.PositionIndex.Add(_name, new Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance));
  }

  IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> IEnumerable<KeyValuePair<VectorKey, VectorEntry<T>>>.GetEnumerator()
  {
    foreach (var K in _store.PositionIndex[_name].Keys)
      yield return new KeyValuePair<VectorKey, VectorEntry<T>>(K,
        new VectorEntry<T>()
        {
          Key = K,
          Content = options.Deserialize(_store.ReadContent(_name, K))
        });

    yield break;
  }

  public IEnumerator<VectorEntry<T>> GetEnumerator()
  {
    foreach (var K in _store.PositionIndex[_name].Keys)
      yield return new VectorEntry<T>()
      {
        Key = K,
        Content = options.Deserialize(_store.ReadContent(_name, K))
      };

    yield break;
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(VectorEntry<T> item)
  {
    if (item == null) return;
    if (_dimensions != item.Embedding.Length) throw new InvalidOperationException($"Dimensions mismatch: expected {_dimensions}, got {item.Embedding.Length}");

    _store.AppendContent(new VectorEntry()
    {
      Id = item.Key.Key,
      CollectionName = this._name,
      Content = options.Serialize(item.Content),
      Embedding = item.Embedding
    }).GetAwaiter().GetResult();
  }

  public void Add(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    this.Add(item.Value);
  }

  public void Clear()
  {
    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      positionIndex.Clear();
  }

  public bool Contains(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    return this.Contains(item.Value);
  }

  public void CopyTo(KeyValuePair<VectorKey, VectorEntry<T>>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    return _store.PositionIndex[_name].Remove(item.Key.Key);
  }

  public bool Contains(VectorEntry<T> item)
  {
    if (item == null) return false;

    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      return positionIndex.ContainsKey(item.Key.Key);
    return false;
  }

  public void CopyTo(VectorEntry<T>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(VectorEntry<T> item)
  {
    if (item == null) return false;
    return _store.PositionIndex.TryGetValue(_name, out var positionIndex) && positionIndex.Remove(item.Key.Key);
  }

  public int Count => _store.PositionIndex.TryGetValue(_name, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, VectorEntry<T> value)
  {
    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      positionIndex.Remove(key.Key);
  }

  public bool ContainsKey(VectorKey key)
  {
    return _store.PositionIndex.TryGetValue(_name, out var positionIndex) && positionIndex.ContainsKey(key.Key);
  }

  public bool Remove(VectorKey key)
  {
    return _store.PositionIndex.TryGetValue(_name, out var positionIndex) && positionIndex.Remove(key.Key);
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      if (positionIndex.ContainsKey(key.Key))
      {
        value = new VectorEntry<T>()
        {
          Key = key.Key,
          Content = options.Deserialize(_store.ReadContent(_name, key.Key))
        };
        return true;
      }

    value = default;
    return false;
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => new()
    {
      Key = key.Key,
      Content = options.Deserialize(_store.ReadContent(_name, key.Key))
    };
    set => this.Add(key, value);
  }

  public ICollection<VectorKey> Keys => _store.PositionIndex.TryGetValue(_name, out var index) ? index.Keys.Select(i => (VectorKey)i).ToList() : new List<VectorKey>();

  public ICollection<VectorEntry<T>> Values => _store.PositionIndex.TryGetValue(_name, out var index)
    ? index.Keys.Select(i =>
      new VectorEntry<T>() { Key = i, Content = options.Deserialize(_store.ReadContent(_name, i)) }).ToList()
    : new List<VectorEntry<T>>();
}