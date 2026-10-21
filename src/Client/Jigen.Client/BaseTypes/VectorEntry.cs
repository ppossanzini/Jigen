namespace Jigen.Client.BaseTypes;

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }
  public float[] Embedding { get; set; }

  /// <summary>
  /// Optional lexical slot, independent of the dense embedding: it is supplied
  /// by the caller and never derived from the content by the server. A null
  /// value stores the entry without one.
  /// </summary>
  public SparseVector Sparse { get; set; }
}