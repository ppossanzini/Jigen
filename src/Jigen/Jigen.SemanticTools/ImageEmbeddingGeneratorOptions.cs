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
