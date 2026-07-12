using System.Collections;
using System.Linq.Expressions;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;

namespace Jigen;

public class DocumentCollection<T>(Store store, DocumentCollectionOptions<T> options = null) :
  IDictionary<VectorKey, T>
  where T : class, new()
{
  private readonly DocumentCollectionOptions<T> _options = options ?? new DocumentCollectionOptions<T>();

  private string CollectionName => _options.Name;

  private IEnumerable<KeyValuePair<VectorKey, T>> EnumerateDocuments(IFilterExpression filter = null)
  {
    var filtered = store.Search(CollectionName, filter);
    foreach (var entry in filtered)
    {
      var document = _options.DocumentSerializer.Deserialize<T>(entry.Content);
      yield return new KeyValuePair<VectorKey, T>(entry.Id, document);
    }
  }

  public IEnumerator<KeyValuePair<VectorKey, T>> GetEnumerator()
  {
    return EnumerateDocuments().GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, T> item)
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

  public bool Contains(KeyValuePair<VectorKey, T> item)
  {
    return ContainsKey(item.Key);
  }

  public void CopyTo(KeyValuePair<VectorKey, T>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, T> item)
  {
    return Remove(item.Key);
  }

  public int Count => store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;

  public void Add(VectorKey key, T value)
  {
    AddAsync(key, value).GetAwaiter().GetResult();
  }

  public Task AddAsync(VectorKey key, T value)
  {
    ArgumentNullException.ThrowIfNull(key.Value);
    ArgumentNullException.ThrowIfNull(value);

    return store.SetContent(new VectorEntry()
    {
      Id = key.Value,
      CollectionName = CollectionName,
      Content = _options.DocumentSerializer.Serialize(value)
    });
  }

  public bool ContainsKey(VectorKey key)
  {
    return store.PositionIndex.TryGetValue(CollectionName, out var index) &&
           index.ContainsKey(key.Value);
  }

  /// <summary>
  /// Searches documents using an Expression predicate.
  /// The expression is translated to a serialized filter and applied at the index level.
  /// </summary>
  public List<KeyValuePair<VectorKey, T>> Search(Expression<Func<T, bool>> predicate = null)
  {
    if (predicate == null)
      return EnumerateDocuments().ToList();

    var filter = ExpressionTranslator.Translate(predicate);
    return Search(filter);
  }

  /// <summary>
  /// Searches documents using a serialized filter.
  /// The filter is applied entirely at the index level without deserialization.
  /// This enables filters to be transferred via GRPC and applied server-side.
  /// </summary>
  public List<KeyValuePair<VectorKey, T>> Search(IFilterExpression filter)
  {
    return EnumerateDocuments(filter).ToList();
  }

  public bool Remove(VectorKey key)
  {
    return RemoveAsync(key).GetAwaiter().GetResult();
  }

  public Task<bool> RemoveAsync(VectorKey key)
  {
    return store.DeleteContent(CollectionName, key.Value);
  }

  public bool TryGetValue(VectorKey key, out T value)
  {
    var result = store.GetContent(CollectionName, key.Value);
    value = result != null ? _options.DocumentSerializer.Deserialize<T>(result) : null;
    return result != null;
  }

  public T this[VectorKey key]
  {
    get => this.TryGetValue(key, out var result) ? result : null;
    set => this.Add(key, value);
  }

  public ICollection<VectorKey> Keys =>
    (store.PositionIndex.TryGetValue(CollectionName, out var index) ? index.Keys.Select(i => (VectorKey)i).ToArray() : null) ?? Array.Empty<VectorKey>();

  public ICollection<T> Values
  {
    get { return EnumerateDocuments().Select(k => k.Value).ToArray(); }
  }
}
