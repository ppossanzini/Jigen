namespace Jigen.SemanticTools;

public interface IEmbeddingGenerator
{
  float[] GenerateEmbedding(string input);
  float[] GenerateEmbedding(string task, string input);
  float[][] GenerateEmbeddings(IReadOnlyList<string> inputs);
  int CountTokens(string input);

  Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
  Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default);
  Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);

  /// <summary>
  /// Generates the sparse lexical vector of a text: its tokenizer terms with
  /// their weights. The same text that produces the dense vector produces this
  /// one, so a caller can fill both slots from a single input.
  /// </summary>
  SparseEmbedding GenerateSparseEmbedding(string input);

  /// <summary>Async counterpart of <see cref="GenerateSparseEmbedding"/>.</summary>
  Task<SparseEmbedding> GenerateSparseEmbeddingAsync(string input, CancellationToken cancellationToken = default);
}
