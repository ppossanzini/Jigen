namespace Jigen.Client.BaseTypes;

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }

  /// <summary>Every vector of the entry; a single-vector entry is a list of one.</summary>
  public IReadOnlyList<float[]> Embeddings { get; set; } = [];

  /// <summary>
  /// Sparse (lexical) terms per vector, aligned by index with
  /// <see cref="Embeddings"/>: term id to weight. Vectors beyond this list are
  /// stored without sparse terms.
  /// </summary>
  public IReadOnlyList<IReadOnlyDictionary<int, float>> SparseVectors { get; set; } = [];
}