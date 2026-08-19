namespace Jigen.SemanticTools;

public sealed class ImageEmbeddingGeneratorOptions
{
  /// <summary>
  /// Target width the input image is resized (and center-cropped) to before
  /// inference. Defaults to the nomic-embed-vision-v1.5 input size (224).
  /// </summary>
  public int InputWidth { get; set; } = 224;

  /// <summary>
  /// Target height the input image is resized (and center-cropped) to before
  /// inference. Defaults to the nomic-embed-vision-v1.5 input size (224).
  /// </summary>
  public int InputHeight { get; set; } = 224;

  /// <summary>
  /// Per-channel RGB normalization mean (ImageNet / CLIP values by default,
  /// matching nomic-embed-vision-v1.5's preprocessor_config.json).
  /// </summary>
  public float[] ImageMean { get; set; } = [0.48145466f, 0.4578275f, 0.40821073f];

  /// <summary>
  /// Per-channel RGB normalization standard deviation (ImageNet / CLIP values
  /// by default, matching nomic-embed-vision-v1.5's preprocessor_config.json).
  /// </summary>
  public float[] ImageStd { get; set; } = [0.26862954f, 0.26130258f, 0.27577711f];

  /// <summary>
  /// Number of threads used by ONNX Runtime for a single inference run.
  /// 0 or negative lets ONNX Runtime decide (all cores), which oversubscribes
  /// the CPU when multiple runs execute concurrently.
  /// </summary>
  public int IntraOpNumThreads { get; set; }

  /// <summary>
  /// Maximum number of images fused into a single ONNX inference run.
  /// 1 disables batching. On CPU the intra-op parallelism already saturates the
  /// cores; raise this when running on a GPU execution provider.
  /// </summary>
  public int MaxBatchSize { get; set; } = 1;

  /// <summary>
  /// Number of tile columns for the tile grid used by
  /// <see cref="IImageEmbeddingGenerator.GenerateImageTileEmbeddings"/>. The
  /// number of rows is derived from the image aspect ratio, so each tile is
  /// square and covers the image with no gaps. Clamped to a minimum of 1.
  /// </summary>
  public int TileColumns { get; set; } = 4;

  /// <summary>
  /// Overlap between adjacent tiles as a fraction of the tile size
  /// (0 = none, 0.2 = 20%). Clamped between 0 and 0.9.
  /// </summary>
  public float TileOverlap { get; set; } = 0.2f;

  /// <summary>
  /// Execution provider for the vision model: "cpu" (default), "cuda", "dml",
  /// "openvino[:DEVICE]" (e.g. "openvino:GPU"), "coreml" (Apple Silicon, included
  /// in the default package), "rocm" or "migraphx" (AMD, require a custom ONNX
  /// Runtime native build). Non-CPU providers may require building with the
  /// matching native runtime package (JigenOnnxRuntimeFlavor MSBuild property)
  /// and fall back to CPU when registration fails.
  /// </summary>
  public string ExecutionProvider { get; set; } = "cpu";

  /// <summary>
  /// Device index used by the "cuda" and "dml" execution providers.
  /// </summary>
  public int GpuDeviceId { get; set; }
}
