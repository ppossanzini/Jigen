using System.Runtime.InteropServices;


using System.Numerics;
using System.Runtime.Intrinsics;
using Jigen;

public static class Extensions
{
  public static VectorCollection<T> VectorCollection<T>(this Store store, string name)
    where T : class, new()
  {
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>(){Name = name});
  }
  
  public static VectorCollection<T> VectorCollection<T>(this Store store)
    where T : class, new()
  {
    var name = typeof(T).Name;
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>(){Name = name});
  }
  
  public static DocumentCollection<T> DocumentCollection<T>(this Store store, string name)
    where T : class, new()
  {
    return new DocumentCollection<T>(store, new DocumentCollectionOptions<T>(){Name = name});
  }
  
  public static DocumentCollection<T> DocumentCollection<T>(this Store store)
    where T : class, new()
  {
    var name = typeof(T).Name;
    return new DocumentCollection<T>(store, new DocumentCollectionOptions<T>(){Name = name});
  }
}