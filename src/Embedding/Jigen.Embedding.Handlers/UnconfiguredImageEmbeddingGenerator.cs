using Jigen.SemanticTools;

namespace Jigen.Embedding.Handlers;

/// <summary>
/// Placeholder returned when no vision model is configured
/// (<c>JigenEmbeddings:ImagesModelPath</c> is empty): keeps text-only
/// deployments working while making image requests fail with a clear
/// configuration error instead of crashing the server at startup.
/// </summary>
public sealed class UnconfiguredImageEmbeddingGenerator : IImageEmbeddingGenerator
{
  private static InvalidOperationException NotConfigured() =>
    new("Image embeddings are not configured. Set JigenEmbeddings:ImagesModelPath to a valid ONNX vision model (e.g. nomic-embed-vision-v1.5).");

  public float[] GenerateImageEmbedding(string imagePath) => throw NotConfigured();
  public float[] GenerateImageEmbedding(byte[] imageBytes) => throw NotConfigured();
  public float[][] GenerateImageEmbeddings(IReadOnlyList<byte[]> images) => throw NotConfigured();
  public float[][] GenerateImageTileEmbeddings(byte[] imageBytes) => throw NotConfigured();

  public Task<float[]> GenerateImageEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default) => throw NotConfigured();
  public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) => throw NotConfigured();
  public Task<float[][]> GenerateImageEmbeddingsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default) => throw NotConfigured();
  public Task<float[][]> GenerateImageTileEmbeddingsAsync(byte[] imageBytes, CancellationToken cancellationToken = default) => throw NotConfigured();
}
