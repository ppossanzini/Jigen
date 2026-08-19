// Image embedding with ONNX Runtime, aligned to the nomic-embed-text-v1.5
// embedding space via nomic-embed-vision-v1.5. Preprocessing follows the
// model's preprocessor_config.json (CLIPImageProcessor): resize to the target
// size (bicubic), center crop (a no-op when it matches the resize target),
// rescale to [0,1] and normalize with the CLIP ImageNet mean/std. The embedding
// is the CLS token (index 0) of last_hidden_state, L2-normalized — the
// reference usage `F.normalize(img_emb[:, 0], p=2, dim=1)`. Because vision and
// text share the same space, image and text vectors can be stored side by side
// in a Jigen collection and compared/searched together.

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Jigen.SemanticTools;

/// <summary>
/// Provides image embeddings using an ONNX vision model (default target:
/// nomic-embed-vision-v1.5) sharing the embedding space of nomic-embed-text-v1.5.
/// </summary>
public sealed class OnnxImageEmbeddingGenerator : IDisposable, IImageEmbeddingGenerator
{
  private readonly InferenceSession _session;
  private readonly ILogger _logger;
  private readonly string _inputName;
  private readonly string _outputName;
  private readonly int _inputWidth;
  private readonly int _inputHeight;
  private readonly float[] _mean;
  private readonly float[] _std;
  private readonly int _maxBatchSize;

  private bool _disposed;

  /// <summary>
  /// Initializes a new instance of the OnnxImageEmbeddingGenerator class.
  /// </summary>
  /// <param name="modelPath">Path to the ONNX vision model.</param>
  public OnnxImageEmbeddingGenerator(
    string modelPath,
    ILogger logger = null,
    ImageEmbeddingGeneratorOptions options = null)
  {
    if (string.IsNullOrWhiteSpace(modelPath))
      throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));

    _logger = logger;
    options ??= new ImageEmbeddingGeneratorOptions();

    _inputWidth = Math.Max(options.InputWidth, 16);
    _inputHeight = Math.Max(options.InputHeight, 16);

    if (options.ImageMean is null || options.ImageMean.Length != 3)
      throw new ArgumentException("ImageMean must contain exactly 3 values (RGB).", nameof(options));
    if (options.ImageStd is null || options.ImageStd.Length != 3)
      throw new ArgumentException("ImageStd must contain exactly 3 values (RGB).", nameof(options));
    _mean = options.ImageMean;
    _std = options.ImageStd;

    _maxBatchSize = Math.Max(options.MaxBatchSize, 1);

    using var sessionOptions = new SessionOptions
    {
      GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
      ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
    };

    if (options.IntraOpNumThreads > 0)
      sessionOptions.IntraOpNumThreads = options.IntraOpNumThreads;

    OnnxExecutionProvider.Append(sessionOptions, options.ExecutionProvider, options.GpuDeviceId, _logger);

    _session = new InferenceSession(modelPath, sessionOptions);
    _inputName = ResolveInputName(_session);
    _outputName = ResolveOutputName(_session);

    _logger?.LogInformation(
      "Loaded image embedding model from path {ModelPath} (input={InputName}[{Width}x{Height}x3], output={OutputName}, intraOpThreads={IntraOpThreads})",
      modelPath,
      _inputName,
      _inputWidth,
      _inputHeight,
      _outputName,
      options.IntraOpNumThreads > 0 ? options.IntraOpNumThreads : Environment.ProcessorCount);
  }

  /// <summary>
  /// Generates an embedding for the image at <paramref name="imagePath"/>.
  /// </summary>
  public float[] GenerateImageEmbedding(string imagePath)
  {
    if (string.IsNullOrWhiteSpace(imagePath))
      throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));

    return GenerateImageEmbedding(File.ReadAllBytes(imagePath));
  }

  /// <summary>
  /// Generates an embedding for an in-memory image.
  /// </summary>
  public float[] GenerateImageEmbedding(byte[] imageBytes)
  {
    ArgumentNullException.ThrowIfNull(imageBytes);
    return GenerateImageEmbeddings([imageBytes])[0];
  }

  /// <summary>
  /// Generates embeddings for multiple images, fusing them into batched
  /// inference runs of up to <see cref="ImageEmbeddingGeneratorOptions.MaxBatchSize"/>.
  /// </summary>
  public float[][] GenerateImageEmbeddings(IReadOnlyList<byte[]> images)
  {
    ArgumentNullException.ThrowIfNull(images);

    if (images.Count == 0)
      return [];

    for (var i = 0; i < images.Count; i++)
    {
      if (images[i] is null || images[i].Length == 0)
        throw new ArgumentException($"Image at index {i} is null or empty.", nameof(images));
    }

    var results = new float[images.Count][];

    for (var start = 0; start < images.Count; start += _maxBatchSize)
    {
      var batchSize = Math.Min(_maxBatchSize, images.Count - start);
      var tensor = new DenseTensor<float>([batchSize, 3, _inputHeight, _inputWidth]);

      for (var i = 0; i < batchSize; i++)
        Preprocess(images[start + i], tensor, i);

      using var modelResults = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
      var rows = ExtractEmbeddingVectors(modelResults.ToList(), batchSize);

      for (var i = 0; i < batchSize; i++)
        results[start + i] = rows[i];
    }

    return results;
  }

  public Task<float[]> GenerateImageEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateImageEmbedding(imagePath), cancellationToken);

  public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateImageEmbedding(imageBytes), cancellationToken);

  public Task<float[][]> GenerateImageEmbeddingsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateImageEmbeddings(images), cancellationToken);

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _session.Dispose();
  }

  /// <summary>
  /// CLIPImageProcessor-equivalent preprocessing: decode, resize to the target
  /// size with bicubic resampling (CLIP resizes to exactly size when both
  /// height and width are given), rescale to [0,1] and normalize per-channel.
  /// The center crop is a no-op here because the resize already produces the
  /// crop size.
  /// </summary>
  private void Preprocess(byte[] imageBytes, DenseTensor<float> tensor, int batchIndex)
  {
    using var image = Image.Load<Rgba32>(imageBytes);

    image.Mutate(context => context.Resize(new ResizeOptions
    {
      Size = new Size(_inputWidth, _inputHeight),
      Mode = ResizeMode.Stretch,
      Sampler = KnownResamplers.Bicubic
    }));

    image.ProcessPixelRows(accessor =>
    {
      for (var y = 0; y < _inputHeight; y++)
      {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < _inputWidth; x++)
        {
          ref var pixel = ref row[x];
          // Rgba32 stores channels in RGBA order; the model expects RGB (NCHW).
          tensor[batchIndex, 0, y, x] = (pixel.R / 255f - _mean[0]) / _std[0];
          tensor[batchIndex, 1, y, x] = (pixel.G / 255f - _mean[1]) / _std[1];
          tensor[batchIndex, 2, y, x] = (pixel.B / 255f - _mean[2]) / _std[2];
        }
      }
    });
  }

  private static float[][] ExtractEmbeddingVectors(IReadOnlyList<DisposableNamedOnnxValue> results, int batchSize)
  {
    // Preferred output: last_hidden_state [B, seq, hidden] — take the CLS token
    // (index 0), matching the reference usage F.normalize(img_emb[:, 0]).
    foreach (var result in results)
    {
      if (!string.Equals(result.Name, "last_hidden_state", StringComparison.OrdinalIgnoreCase))
        continue;

      if (TryExtractClsRows(result, batchSize, out var clsRows))
        return clsRows;
    }

    // Fallback: a pooled [B, hidden] output.
    foreach (var result in results)
    {
      if (TryExtractPooledRows(result, batchSize, out var pooled))
        return pooled;
    }

    // Last-resort fallback (unknown output layout): only safe without batching,
    // because it cannot be attributed to individual rows.
    if (batchSize == 1)
    {
      foreach (var result in results.Reverse())
      {
        try
        {
          return [L2Normalize(result.AsTensor<float>().ToArray())];
        }
        catch
        {
        }
      }
    }

    var empty = new float[batchSize][];
    for (var i = 0; i < batchSize; i++)
      empty[i] = Array.Empty<float>();
    return empty;
  }

  private static bool TryExtractClsRows(DisposableNamedOnnxValue output, int batchSize, out float[][] rows)
  {
    rows = null;

    try
    {
      var tensor = output.AsTensor<float>();
      var dims = tensor.Dimensions;
      if (dims.Length != 3 || dims[0] != batchSize || dims[1] < 1 || dims[2] <= 0)
        return false;

      var sequenceLength = (int)dims[1];
      var hiddenSize = (int)dims[2];
      var values = tensor.ToArray();
      rows = new float[batchSize][];

      for (var row = 0; row < batchSize; row++)
      {
        var cls = new float[hiddenSize];
        Array.Copy(values, row * sequenceLength * hiddenSize, cls, 0, hiddenSize);
        rows[row] = L2Normalize(cls);
      }

      return true;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryExtractPooledRows(DisposableNamedOnnxValue output, int batchSize, out float[][] rows)
  {
    rows = null;

    try
    {
      var tensor = output.AsTensor<float>();
      var dims = tensor.Dimensions;
      if (dims.Length != 2 || dims[0] != batchSize || dims[1] <= 0)
        return false;

      var hiddenSize = (int)dims[1];
      var values = tensor.ToArray();
      rows = new float[batchSize][];

      for (var row = 0; row < batchSize; row++)
      {
        var pooled = new float[hiddenSize];
        Array.Copy(values, row * hiddenSize, pooled, 0, hiddenSize);
        rows[row] = L2Normalize(pooled);
      }

      return true;
    }
    catch
    {
      return false;
    }
  }

  private static float[] L2Normalize(float[] vector)
  {
    var normSquared = 0f;
    foreach (var value in vector)
      normSquared += value * value;

    if (normSquared <= 0f)
      return vector;

    var invNorm = 1f / MathF.Sqrt(normSquared);
    for (var i = 0; i < vector.Length; i++)
      vector[i] *= invNorm;

    return vector;
  }

  private static string ResolveInputName(InferenceSession session)
  {
    if (session.InputMetadata.Count == 0)
      throw new InvalidOperationException("Image embedding ONNX model exposes no inputs.");

    return session.InputMetadata.Keys.First();
  }

  private static string ResolveOutputName(InferenceSession session)
  {
    if (session.OutputMetadata.Count == 0)
      throw new InvalidOperationException("Image embedding ONNX model exposes no outputs.");

    foreach (var key in session.OutputMetadata.Keys)
    {
      if (string.Equals(key, "last_hidden_state", StringComparison.OrdinalIgnoreCase))
        return key;
    }

    return session.OutputMetadata.Keys.First();
  }
}
