using Google.Protobuf.Collections;

namespace Jigen.Client;

public static class Extensions
{
  public static RepeatedField<T> Append<T>(this RepeatedField<T> item, IEnumerable<T> values)
  {
    item.AddRange(values);
    return item;
  }
  
  public static VectorCollection<T> Collection<T>(this Context store, string name)
  where T : class, new()
  {
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>(){Name = name});
  }
  
  public static VectorCollection<T> Collection<T>(this Context store)
    where T : class, new()
  {
    var name = typeof(T).Name;
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>(){Name = name});
  }
}