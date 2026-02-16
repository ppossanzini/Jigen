using Google.Protobuf.Collections;

namespace Jigen.Client;

public static class Extensions
{
  public static RepeatedField<T> Append<T>(this RepeatedField<T> item, IEnumerable<T> values)
  {
    item.AddRange(values);
    return item;
  }
  
}