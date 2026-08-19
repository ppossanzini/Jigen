namespace Jigen.SemanticTools;

/// <summary>
/// Generates image embeddings with a vision model aligned to a text embedding
/// space (e.g. nomic-embed-vision-v1.5, aligned to nomic-embed-text-v1.5), so
/// image and text vectors can be compared and searched together in a Jigen
/// collection.
/// </summary>
public interface IImageEmbeddingGenerator
{
  float[] GenerateImageEmbedding(string imagePath);
  float[] GenerateImageEmbedding(byte[] imageBytes);
  float[][] GenerateImageEmbeddings(IReadOnlyList<byte[]> images);

  Task<float[]> GenerateImageEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default);
  Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
  Task<float[][]> GenerateImageEmbeddingsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default);
}
