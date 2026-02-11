namespace Jigen.DataStructures;

public class Document<T>
{
  public virtual VectorKey Key { get; set; }
  public T Value { get; set; }
}