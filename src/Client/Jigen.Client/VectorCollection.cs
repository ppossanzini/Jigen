using System.Collections;
using Jigen.Client.BaseTypes;

namespace Jigen.Client;

public class VectorCollection<T>(Context store, VectorCollectionOptions<T> options = null) :
  IDictionary<VectorKey, VectorEntry<T>>
  where T : class, new()
{
  public IEnumerator<KeyValuePair<VectorKey, VectorEntry<T>>> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    throw new NotImplementedException();
  }

  public void Clear()
  {
    throw new NotImplementedException();
  }

  public bool Contains(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<VectorKey, VectorEntry<T>>[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(KeyValuePair<VectorKey, VectorEntry<T>> item)
  {
    throw new NotImplementedException();
  }

  public int Count { get; }
  public bool IsReadOnly { get; }
  public void Add(VectorKey key, VectorEntry<T> value)
  {
    throw new NotImplementedException();
  }

  public bool ContainsKey(VectorKey key)
  {
    throw new NotImplementedException();
  }

  public bool Remove(VectorKey key)
  {
    throw new NotImplementedException();
  }

  public bool TryGetValue(VectorKey key, out VectorEntry<T> value)
  {
    throw new NotImplementedException();
  }

  public VectorEntry<T> this[VectorKey key]
  {
    get => throw new NotImplementedException();
    set => throw new NotImplementedException();
  }

  public ICollection<VectorKey> Keys { get; }
  public ICollection<VectorEntry<T>> Values { get; }
}