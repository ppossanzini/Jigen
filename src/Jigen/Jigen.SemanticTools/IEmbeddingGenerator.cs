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
  /// Generates the sparse vector of a text: its tokenizer terms with their
  /// weights (lexical, or learned when the model has a sparse head). The same
  /// text that produces the dense vector produces this one, so a caller can fill
  /// both slots from a single input.
  /// </summary>
  SparseEmbedding GenerateSparseEmbedding(string input);

  /// <summary>Async counterpart of <see cref="GenerateSparseEmbedding"/>.</summary>
  Task<SparseEmbedding> GenerateSparseEmbeddingAsync(string input, CancellationToken cancellationToken = default);

  /// <summary>
  /// Dense and sparse vector of the same text. A generator with a learned sparse
  /// head computes both from one inference; the default runs the two separately.
  /// </summary>
  async Task<(float[] Dense, SparseEmbedding Sparse)> GenerateEmbeddingWithSparseAsync(string input,
    CancellationToken cancellationToken = default)
  {
    var dense = GenerateEmbeddingAsync(input, cancellationToken);
    var sparse = await GenerateSparseEmbeddingAsync(input, cancellationToken).ConfigureAwait(false);
    return (await dense.ConfigureAwait(false), sparse);
  }
}
