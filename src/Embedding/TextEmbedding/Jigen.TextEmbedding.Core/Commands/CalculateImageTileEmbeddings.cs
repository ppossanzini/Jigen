using Hikyaku;

namespace Jigen.TextEmbedding.Core.Commands;

/// <summary>
/// Requests the tile embeddings of a single image: equally sized, overlapping
/// tiles each embedded separately, plus the whole-image embedding appended as
/// the last vector. Tiling parameters come from the server configuration
/// (ImageGeneratorOptions); the result is one L2-normalized vector per tile
/// plus the global vector, in raster order.
/// </summary>
public class CalculateImageTileEmbeddings : IRequest<float[][]>
{
  public byte[] ImageBytes { get; set; }
}
