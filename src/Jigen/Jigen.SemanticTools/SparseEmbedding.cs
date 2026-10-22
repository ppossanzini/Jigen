namespace Jigen.SemanticTools;

/// <summary>
/// A sparse lexical vector produced from text: term ids (tokenizer vocabulary
/// ids) with their weights. Absent terms are zero, so only the non-zero entries
/// are carried. Terms are unique and strictly increasing, ready for
/// <c>Jigen.DataStructures.SparseVector</c> without further normalisation.
/// </summary>
/// <param name="Terms">Vocabulary ids, strictly increasing.</param>
/// <param name="Weights">One per term, in the same order, all above zero.</param>
public readonly record struct SparseEmbedding(int[] Terms, float[] Weights)
{
  public int Count => Terms?.Length ?? 0;

  public static SparseEmbedding Empty => new([], []);
}

/// <summary>
/// Configuration of the sparse side of an embedding model.
/// </summary>
public class SparseEmbeddingOptions
{
  /// <summary>
  /// Maximum number of terms kept in one sparse vector. When a text yields more
  /// distinct terms, the heaviest are kept and the rest dropped. Align it with
  /// the store's <c>SparseOptions.MaxTerms</c>, which refuses a longer vector.
  /// </summary>
  public int MaxTerms { get; set; } = 1024;

  /// <summary>
  /// Weight of a term appearing once is <c>Offset</c>; every further occurrence
  /// adds <c>Log</c>-damped weight. The default scores a term once at 1.0 and
  /// three times at ~2.1, which keeps a repeated word meaningful without letting
  /// repetition dominate the dot product.
  /// </summary>
  public float Offset { get; set; } = 1f;

  /// <summary>Damping of repeated occurrences; must be greater than zero.</summary>
  public float LogBase { get; set; } = 1f;
}
