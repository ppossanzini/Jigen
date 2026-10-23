namespace Jigen.Client.BaseTypes;

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }
  public float[] Embedding { get; set; }

  /// <summary>Every vector of a multi-vector entry; overrides <see cref="Embedding"/> when set.</summary>
  public IReadOnlyList<float[]> Embeddings { get; set; } = [];
}