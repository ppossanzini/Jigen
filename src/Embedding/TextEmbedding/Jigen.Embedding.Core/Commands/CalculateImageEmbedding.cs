using Hikyaku;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Requests the embedding of a single image, computed by the vision model
/// aligned to the text embedding space (e.g. nomic-embed-vision-v1.5), so image
/// and text vectors can be compared and searched together. The raw image bytes
/// are carried natively and serialized by the channel; base64 encoding is only
/// needed at the HTTP/JSON boundary.
/// </summary>
public class CalculateImageEmbedding : IRequest<float[]>
{
  public byte[] ImageBytes { get; set; }
}
