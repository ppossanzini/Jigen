namespace Jigen.SemanticTools;

/// <summary>
/// A sparse lexical vector produced from text: term ids (tokenizer vocabulary
/// ids) with their weights. Absent terms are zero, so only the non-zero entries
/// are carried. Terms are unique and strictly increasing, ready for
/// <c>Jigen.DataStructures.SparseTerm</c>.
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
public sealed class SparseEmbeddingOptions
{
  /// <summary>
  /// Maximum number of terms kept in one sparse vector. When a text yields more
  /// distinct terms, the heaviest are kept and the rest dropped.
  /// </summary>
  public int MaxTerms { get; set; } = 1024;

  /// <summary>
  /// Weight of a term appearing once is <see cref="Offset"/>; every further
  /// occurrence adds <see cref="LogBase"/>-scaled log damping, so a repeated word
  /// counts more without dominating the dot product.
  /// </summary>
  public float Offset { get; set; } = 1f;

  /// <summary>Damping of repeated occurrences; must be greater than zero.</summary>
  public float LogBase { get; set; } = 1f;

  /// <summary>
  /// Path of the learned sparse head (<c>sparse_linear.pt</c> from <c>BAAI/bge-m3</c>),
  /// <c>BgeM3</c> profile only. A relative path is resolved against the model
  /// directory. When set, the weights come from the model instead of from term
  /// frequencies (<see cref="Offset"/> and <see cref="LogBase"/> are then unused).
  /// Empty keeps the lexical weights.
  /// </summary>
  public string HeadPath { get; set; }
}
