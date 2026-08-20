using Hikyaku;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Batch counterpart of <see cref="CalculateImageEmbedding"/>: one request (and,
/// with a remote embedding worker, one dispatch) for many images, carried as
/// raw bytes and serialized natively by the channel. The result has one row per
/// image, in the same order.
/// </summary>
public class CalculateImageEmbeddingBatch : IRequest<float[][]>
{
  public byte[][] Images { get; set; }
}
