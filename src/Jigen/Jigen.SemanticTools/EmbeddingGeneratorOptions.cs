namespace Jigen.SemanticTools;

public sealed class EmbeddingGeneratorOptions
{
  public int MaxTokens { get; set; } = 384;
  public bool UseChunking { get; set; } = true;
  public int ChunkSize { get; set; } = 320;
  public int ChunkOverlap { get; set; } = 64;
  public int HeadTailHeadTokens { get; set; } = 256;

  /// <summary>
  /// Number of threads used by ONNX Runtime for a single inference run.
  /// 0 or negative lets ONNX Runtime decide (all cores), which oversubscribes
  /// the CPU when multiple runs execute concurrently.
  /// </summary>
  public int IntraOpNumThreads { get; set; }

  /// <summary>
  /// Maximum number of token sequences fused into a single ONNX inference run.
  /// 1 disables batching. On CPU the intra-op parallelism already saturates the
  /// cores and padding wastes compute on mixed-length inputs, so fusing rarely
  /// helps; raise this when running on a GPU execution provider.
  /// </summary>
  public int MaxBatchSize { get; set; } = 1;
}