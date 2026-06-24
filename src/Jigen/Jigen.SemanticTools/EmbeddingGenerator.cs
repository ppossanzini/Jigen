// This Class is from https://github.com/yuniko-software/tokenizer-to-onnx-model sample

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Jigen.SemanticTools;

/// <summary>
/// Provides functionality to generate embeddings using ONNX tokenizer and embedding models.
/// </summary>
public class OnnxEmbeddingGenerator : IDisposable, IEmbeddingGenerator
{
  private readonly InferenceSession _tokenizerSession;
  private readonly InferenceSession _modelSession;
  private readonly ILogger _logger;
  private readonly bool _requiresTokenTypeIds;
  private readonly int _maxTokens;
  private readonly bool _useChunking;
  private readonly int _chunkSize;
  private readonly int _chunkOverlap;
  private readonly int _headTokens;

  /// <summary>
  /// Initializes a new instance of the OnnxEmbeddingGenerator class.
  /// </summary>
  /// <param name="tokenizerPath">Path to the ONNX tokenizer model.</param>
  /// <param name="modelPath">Path to the ONNX embedding model.</param>
  public OnnxEmbeddingGenerator(
    string tokenizerPath,
    string modelPath,
    ILogger logger = null,
    EmbeddingGeneratorOptions options= null)
  {
    // Initialize tokenizer session with ONNX Extensions
    var tokenizerOptions = new SessionOptions();
    tokenizerOptions.RegisterOrtExtensions();

    _tokenizerSession = new InferenceSession(tokenizerPath, tokenizerOptions);
    _modelSession = new InferenceSession(modelPath);
    _logger = logger;
    _requiresTokenTypeIds = _modelSession.InputMetadata.Keys.Contains("token_type_ids", StringComparer.OrdinalIgnoreCase);

    if (options == null) options = new EmbeddingGeneratorOptions();
    
    _maxTokens = Math.Max(options.MaxTokens, 8);
    _useChunking = options.UseChunking;
    _chunkSize = Math.Clamp(options.ChunkSize, 8, _maxTokens);
    _chunkOverlap = Math.Clamp(options.ChunkOverlap, 0, _chunkSize - 1);
    _headTokens = Math.Clamp(options.HeadTailHeadTokens, 1, _maxTokens - 1);
  }

  /// <summary>
  /// Generates embedding for the input text.
  /// </summary>
  /// <param name="text">The input text.</param>
  /// <returns>The embedding vector as a float array.</returns>
  public float[] GenerateEmbedding(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      throw new ArgumentException("Input text cannot be null or empty.", nameof(text));

    try
    {
      var tokenIds = TokenizeToInputIds(text);
      _logger?.LogDebug("Tokenized text chars={Chars}, tokens={Tokens}", text.Length, tokenIds.Length);

      if (tokenIds.Length == 0)
        return Array.Empty<float>();

      if (tokenIds.Length <= _maxTokens)
        return RunModel(tokenIds);

      if (!_useChunking)
      {
        var truncated = TruncateHeadTail(tokenIds);
        _logger?.LogInformation(
          "Input tokens exceed max tokens. Using head-tail truncation: original={OriginalTokens}, truncated={TruncatedTokens}",
          tokenIds.Length,
          truncated.Length);
        return RunModel(truncated);
      }

      var chunks = BuildChunks(tokenIds);
      _logger?.LogInformation(
        "Input tokens exceed max tokens. Using chunking: original={OriginalTokens}, chunks={ChunkCount}, chunkSize={ChunkSize}, overlap={ChunkOverlap}",
        tokenIds.Length,
        chunks.Count,
        _chunkSize,
        _chunkOverlap);

      var vectors = new List<(float[] Vector, int Weight)>(chunks.Count);
      foreach (var chunk in chunks)
      {
        var vector = RunModel(chunk);
        if (vector.Length > 0)
          vectors.Add((vector, chunk.Length));
      }

      if (vectors.Count == 0)
        return Array.Empty<float>();

      if (vectors.Count == 1)
        return vectors[0].Vector;

      return WeightedAverage(vectors);
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Error while generating embedding. The input may exceed model token limits.");
      return Array.Empty<float>();
    }
  }

  private long[] TokenizeToInputIds(string text)
  {
    var tokenizerInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor("inputs", new DenseTensor<string>([1])
      {
        [0] = text
      })
    };

    using var tokenizerResults = _tokenizerSession.Run(tokenizerInputs);
    var tokenizerResultsList = tokenizerResults.ToList();

    var tokens = tokenizerResultsList[0].AsTensor<int>().ToArray();
    var tokenIndices = tokenizerResultsList[2].AsTensor<int>().ToArray();

    return tokens
      .Zip(tokenIndices, (token, index) => (token, index))
      .OrderBy(item => item.index)
      .Select(item => (long)item.token)
      .ToArray();
  }

  private float[] RunModel(long[] tokenIds)
  {
    var inputIdsTensor = new DenseTensor<long>(tokenIds, [1, tokenIds.Length], false);
    var attentionMaskTensor = new DenseTensor<long>([1, tokenIds.Length]);
    attentionMaskTensor.Fill(1);

    var modelInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
      NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
    };

    if (_requiresTokenTypeIds)
    {
      var tokenTypeIdsTensor = new DenseTensor<long>([1, tokenIds.Length]);
      tokenTypeIdsTensor.Fill(0);
      modelInputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
    }

    using var modelResults = _modelSession.Run(modelInputs);
    var results = modelResults.ToList();
    return ExtractEmbeddingVector(results, tokenIds.Length);
  }

  private float[] ExtractEmbeddingVector(IReadOnlyList<DisposableNamedOnnxValue> results, int tokenCount)
  {
    foreach (var result in results)
    {
      if (!string.Equals(result.Name, "sentence_embedding", StringComparison.OrdinalIgnoreCase))
        continue;

      if (TryExtractRank2Vector(result, out var sentenceEmbedding))
        return sentenceEmbedding;
    }

    foreach (var result in results)
    {
      if (TryExtractRank2Vector(result, out var pooled))
        return pooled;
    }

    foreach (var result in results)
    {
      if (TryExtractRank3MeanPooledVector(result, tokenCount, out var meanPooled))
        return meanPooled;
    }

    foreach (var result in results.Reverse())
    {
      try
      {
        return result.AsTensor<float>().ToArray();
      }
      catch
      {
      }
    }

    _logger?.LogWarning("No float tensor output found in ONNX model results.");
    return Array.Empty<float>();
  }

  private static bool TryExtractRank2Vector(DisposableNamedOnnxValue output, out float[] vector)
  {
    vector = null;

    try
    {
      var tensor = output.AsTensor<float>();
      var dims = tensor.Dimensions;
      if (dims.Length != 2 || dims[0] != 1 || dims[1] <= 0)
        return false;

      vector = tensor.ToArray();
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryExtractRank3MeanPooledVector(DisposableNamedOnnxValue output, int tokenCount, out float[] vector)
  {
    vector = null;

    try
    {
      var tensor = output.AsTensor<float>();
      var dims = tensor.Dimensions;
      if (dims.Length != 3 || dims[0] != 1 || dims[1] <= 0 || dims[2] <= 0)
        return false;

      var sequenceLength = (int)dims[1];
      var hiddenSize = (int)dims[2];
      var effectiveTokens = Math.Clamp(tokenCount, 1, sequenceLength);
      var values = tensor.ToArray();
      var pooled = new float[hiddenSize];

      for (var token = 0; token < effectiveTokens; token++)
      {
        var offset = token * hiddenSize;
        for (var i = 0; i < hiddenSize; i++)
          pooled[i] += values[offset + i];
      }

      var divisor = 1f / effectiveTokens;
      for (var i = 0; i < hiddenSize; i++)
        pooled[i] *= divisor;

      vector = pooled;
      return true;
    }
    catch
    {
      return false;
    }
  }

  private long[] TruncateHeadTail(long[] tokenIds)
  {
    if (tokenIds.Length <= _maxTokens)
      return tokenIds;

    var tailCount = _maxTokens - _headTokens;
    if (tailCount <= 0)
      return tokenIds.Take(_maxTokens).ToArray();

    var truncated = new long[_maxTokens];
    Array.Copy(tokenIds, 0, truncated, 0, _headTokens);
    Array.Copy(tokenIds, tokenIds.Length - tailCount, truncated, _headTokens, tailCount);
    return truncated;
  }

  private List<long[]> BuildChunks(long[] tokenIds)
  {
    var chunks = new List<long[]>();
    var step = _chunkSize - _chunkOverlap;

    for (var start = 0; start < tokenIds.Length; start += step)
    {
      var length = Math.Min(_chunkSize, tokenIds.Length - start);
      var chunk = new long[length];
      Array.Copy(tokenIds, start, chunk, 0, length);
      chunks.Add(chunk);

      if (start + length >= tokenIds.Length)
        break;
    }

    return chunks;
  }

  private static float[] WeightedAverage(List<(float[] Vector, int Weight)> vectors)
  {
    var dimension = vectors[0].Vector.Length;
    var accumulator = new double[dimension];
    var totalWeight = 0d;

    foreach (var (vector, weight) in vectors)
    {
      if (weight <= 0 || vector.Length != dimension)
        continue;

      for (var i = 0; i < dimension; i++)
        accumulator[i] += vector[i] * weight;

      totalWeight += weight;
    }

    if (totalWeight <= 0)
      return vectors[0].Vector;

    var pooled = new float[dimension];
    for (var i = 0; i < dimension; i++)
      pooled[i] = (float)(accumulator[i] / totalWeight);

    return pooled;
  }


  public void Dispose()
  {
    _tokenizerSession.Dispose();
    _modelSession.Dispose();
  }
}