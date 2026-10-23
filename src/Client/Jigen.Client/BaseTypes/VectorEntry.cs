namespace Jigen.Client.BaseTypes;

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }

  /// <summary>Every vector of the entry; a single-vector entry is a list of one.</summary>
  public IReadOnlyList<float[]> Embeddings { get; set; } = [];
}