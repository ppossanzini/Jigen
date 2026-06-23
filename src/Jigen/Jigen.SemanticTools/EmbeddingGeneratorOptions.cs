namespace Jigen.SemanticTools;

public sealed class EmbeddingGeneratorOptions
{
  public int MaxTokens { get; init; } = 384;
  public bool UseChunking { get; init; } = true;
  public int ChunkSize { get; init; } = 320;
  public int ChunkOverlap { get; init; } = 64;
  public int HeadTailHeadTokens { get; init; } = 256;
}