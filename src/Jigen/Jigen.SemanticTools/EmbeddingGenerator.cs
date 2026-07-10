// This Class is from https://github.com/yuniko-software/tokenizer-to-onnx-model sample

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Jigen.SemanticTools;

/// <summary>
/// Provides functionality to generate embeddings using ONNX tokenizer and embedding models.
/// </summary>
public class OnnxEmbeddingGenerator : IDisposable, IEmbeddingGenerator
{
  private readonly InferenceSession _tokenizerSession;
  private readonly Tokenizer _jsonTokenizer;
  private readonly string _tokenizerInputName;

  private readonly InferenceSession _modelSession;
  private readonly ILogger _logger;
  private readonly bool _requiresTokenTypeIds;
  private readonly int _maxTokens;
  private readonly bool _useChunking;
  private readonly int _chunkSize;
  private readonly int _chunkOverlap;
  private readonly int _headTokens;
  private readonly int _maxBatchSize;

  /// <summary>
  /// Initializes a new instance of the OnnxEmbeddingGenerator class.
  /// </summary>
  /// <param name="tokenizerPath">Path to the ONNX tokenizer model.</param>
  /// <param name="modelPath">Path to the ONNX embedding model.</param>
  public OnnxEmbeddingGenerator(
    string tokenizerPath,
    string modelPath,
    ILogger logger = null,
    EmbeddingGeneratorOptions options = null)
  {
    _logger = logger;
    options ??= new EmbeddingGeneratorOptions();

    if (tokenizerPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
      _jsonTokenizer = CreateSentencePieceTokenizerFromJsonPath(tokenizerPath);
      _logger?.LogInformation("Loaded tokenizer from JSON path {TokenizerPath}", tokenizerPath);
    }
    else
    {
      // Initialize tokenizer session with ONNX Extensions
      using var tokenizerOptions = new SessionOptions { IntraOpNumThreads = 1 };
      tokenizerOptions.RegisterOrtExtensions();

      _tokenizerSession = new InferenceSession(tokenizerPath, tokenizerOptions);
      _tokenizerInputName = ResolveTokenizerInputName(_tokenizerSession);
      _logger?.LogInformation("Loaded tokenizer ONNX from path {TokenizerPath}", tokenizerPath);
    }

    using var modelSessionOptions = new SessionOptions
    {
      GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
      ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
    };

    if (options.IntraOpNumThreads > 0)
      modelSessionOptions.IntraOpNumThreads = options.IntraOpNumThreads;

    AppendExecutionProvider(modelSessionOptions, options);

    _modelSession = new InferenceSession(modelPath, modelSessionOptions);
    _requiresTokenTypeIds = _modelSession.InputMetadata.Keys.Contains("token_type_ids", StringComparer.OrdinalIgnoreCase);

    _logger?.LogInformation(
      "Loaded embedding model from path {ModelPath} (intraOpThreads={IntraOpThreads})",
      modelPath,
      options.IntraOpNumThreads > 0 ? options.IntraOpNumThreads : Environment.ProcessorCount);

    _maxTokens = Math.Max(options.MaxTokens, 8);
    _useChunking = options.UseChunking;
    _chunkSize = Math.Clamp(options.ChunkSize, 8, _maxTokens);
    _chunkOverlap = Math.Clamp(options.ChunkOverlap, 0, _chunkSize - 1);
    _headTokens = Math.Clamp(options.HeadTailHeadTokens, 1, _maxTokens - 1);
    _maxBatchSize = Math.Max(options.MaxBatchSize, 1);
  }

  /// <summary>
  /// Generates embedding for the input text.
  /// </summary>
  /// <param name="text">The input text.</param>
  /// <returns>The embedding vector as a float array.</returns>
  public float[] GenerateEmbedding(string text)
  {
    return GenerateEmbeddings([text])[0];
  }

  public float[] GenerateEmbedding(string task, string input) =>
    GenerateEmbedding(!string.IsNullOrWhiteSpace(task) ? $"{task}: {input}" : input);

  public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateEmbedding(input), cancellationToken);

  public Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateEmbedding(task, input), cancellationToken);

  public Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
    Task.Run(() => GenerateEmbeddings(inputs), cancellationToken);

  /// <summary>
  /// Generates embeddings for multiple input texts, fusing token sequences
  /// (texts and chunks of long texts) into batched inference runs.
  /// </summary>
  public float[][] GenerateEmbeddings(IReadOnlyList<string> texts)
  {
    ArgumentNullException.ThrowIfNull(texts);

    if (texts.Count == 0)
      return [];

    // Plan: for each text, the token sequences to infer (one sequence, or its chunks).
    var sequences = new List<(int TextIndex, long[] Tokens)>(texts.Count);

    for (var i = 0; i < texts.Count; i++)
    {
      if (string.IsNullOrWhiteSpace(texts[i]))
        throw new ArgumentException("Input text cannot be null or empty.", nameof(texts));

      var tokenIds = TokenizeToInputIds(texts[i]);
      _logger?.LogDebug("Tokenized text chars={Chars}, tokens={Tokens}", texts[i].Length, tokenIds.Length);

      if (tokenIds.Length == 0)
        continue;

      if (tokenIds.Length <= _maxTokens)
      {
        sequences.Add((i, tokenIds));
      }
      else if (!_useChunking)
      {
        var truncated = TruncateHeadTail(tokenIds);
        _logger?.LogInformation(
          "Input tokens exceed max tokens. Using head-tail truncation: original={OriginalTokens}, truncated={TruncatedTokens}",
          tokenIds.Length,
          truncated.Length);
        sequences.Add((i, truncated));
      }
      else
      {
        var chunks = BuildChunks(tokenIds);
        _logger?.LogInformation(
          "Input tokens exceed max tokens. Using chunking: original={OriginalTokens}, chunks={ChunkCount}, chunkSize={ChunkSize}, overlap={ChunkOverlap}",
          tokenIds.Length,
          chunks.Count,
          _chunkSize,
          _chunkOverlap);
        foreach (var chunk in chunks)
          sequences.Add((i, chunk));
      }
    }

    // Batch sequences contiguous by length, so padding to the longest row is minimal.
    var order = Enumerable.Range(0, sequences.Count)
      .OrderByDescending(s => sequences[s].Tokens.Length)
      .ToArray();

    var vectorsByText = new List<(float[] Vector, int Weight)>[texts.Count];

    for (var start = 0; start < order.Length; start += _maxBatchSize)
    {
      var batchSize = Math.Min(_maxBatchSize, order.Length - start);
      var batchTokens = new long[batchSize][];
      for (var j = 0; j < batchSize; j++)
        batchTokens[j] = sequences[order[start + j]].Tokens;

      var rows = RunModelBatch(batchTokens);

      for (var j = 0; j < batchSize; j++)
      {
        if (rows[j].Length == 0)
          continue;

        var (textIndex, tokens) = sequences[order[start + j]];
        (vectorsByText[textIndex] ??= []).Add((rows[j], tokens.Length));
      }
    }

    var results = new float[texts.Count][];
    for (var i = 0; i < texts.Count; i++)
    {
      results[i] = vectorsByText[i] switch
      {
        null or { Count: 0 } => [],
        { Count: 1 } vectors => vectors[0].Vector,
        var vectors => WeightedAverage(vectors)
      };
    }

    return results;
  }


  private long[] TokenizeToInputIds(string text)
  {
    if (_jsonTokenizer != null)
    {
      var ids = _jsonTokenizer.EncodeToIds(text, true, true);
      return ids.Select(static id => (long)id).ToArray();
    }

    var tokenizerInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor(_tokenizerInputName, new DenseTensor<string>([1])
      {
        [0] = text
      })
    };

    using var tokenizerResults = _tokenizerSession.Run(tokenizerInputs);
    var tokenizerResultsList = tokenizerResults.ToList();

    var idsOutput = tokenizerResultsList.FirstOrDefault(output =>
      string.Equals(output.Name, "input_ids", StringComparison.OrdinalIgnoreCase) ||
      string.Equals(output.Name, "ids", StringComparison.OrdinalIgnoreCase) ||
      string.Equals(output.Name, "token_ids", StringComparison.OrdinalIgnoreCase));

    if (idsOutput != null && TryReadLongTensor(idsOutput, out var directIds))
      return directIds;

    // Backward compatibility for tokenizer-to-onnx-model layout: [tokens, ..., tokenIndices]
    if (tokenizerResultsList.Count >= 3 &&
        TryReadIntTensor(tokenizerResultsList[0], out var tokens) &&
        TryReadIntTensor(tokenizerResultsList[2], out var tokenIndices) &&
        tokens.Length == tokenIndices.Length)
    {
      return tokens
        .Zip(tokenIndices, (token, index) => (token, index))
        .OrderBy(item => item.index)
        .Select(item => (long)item.token)
        .ToArray();
    }

    _logger?.LogError(
      "Unsupported tokenizer ONNX output layout. Available outputs: {Outputs}",
      string.Join(", ", tokenizerResultsList.Select(output => output.Name)));

    return Array.Empty<long>();
  }

  private void AppendExecutionProvider(SessionOptions sessionOptions, EmbeddingGeneratorOptions options)
  {
    var provider = options.ExecutionProvider?.Trim();
    if (string.IsNullOrEmpty(provider) || provider.Equals("cpu", StringComparison.OrdinalIgnoreCase))
      return;

    try
    {
      if (provider.Equals("cuda", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_CUDA(options.GpuDeviceId);
      }
      else if (provider.Equals("dml", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_DML(options.GpuDeviceId);
      }
      else if (provider.StartsWith("openvino", StringComparison.OrdinalIgnoreCase))
      {
        var separatorIndex = provider.IndexOf(':');
        var device = separatorIndex >= 0 ? provider[(separatorIndex + 1)..] : "GPU";
        sessionOptions.AppendExecutionProvider_OpenVINO(device);
      }
      else if (provider.Equals("coreml", StringComparison.OrdinalIgnoreCase))
      {
        // MLProgram targets the modern CoreML format; ALL lets CoreML pick
        // between ANE, GPU and CPU per operator.
        sessionOptions.AppendExecutionProvider("CoreML", new Dictionary<string, string>
        {
          ["ModelFormat"] = "MLProgram",
          ["MLComputeUnits"] = "ALL"
        });
      }
      else if (provider.Equals("rocm", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_ROCm(options.GpuDeviceId);
      }
      else if (provider.Equals("migraphx", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_MIGraphX(options.GpuDeviceId);
      }
      else
      {
        _logger?.LogWarning("Unknown execution provider '{Provider}'. Using CPU.", provider);
        return;
      }

      _logger?.LogInformation("Registered execution provider {Provider} for the embedding model.", provider);
    }
    catch (Exception ex)
    {
      _logger?.LogWarning(
        ex,
        "Failed to register execution provider '{Provider}'. Falling back to CPU. " +
        "Ensure the process was built with the matching ONNX Runtime native package (JigenOnnxRuntimeFlavor).",
        provider);
    }
  }

  private static string ResolveTokenizerInputName(InferenceSession tokenizerSession)
  {
    var stringInput = tokenizerSession.InputMetadata
      .FirstOrDefault(input => input.Value.ElementDataType == TensorElementType.String);

    if (!string.IsNullOrWhiteSpace(stringInput.Key))
      return stringInput.Key;

    if (tokenizerSession.InputMetadata.Count == 1)
      return tokenizerSession.InputMetadata.Keys.First();

    throw new InvalidOperationException(
      "Tokenizer ONNX input name could not be resolved automatically. Ensure the model exposes a single string input.");
  }

  private static bool TryReadLongTensor(DisposableNamedOnnxValue value, out long[] result)
  {
    try
    {
      result = value.AsTensor<long>().ToArray();
      return true;
    }
    catch
    {
      result = null;
      return false;
    }
  }

  private static bool TryReadIntTensor(DisposableNamedOnnxValue value, out int[] result)
  {
    try
    {
      result = value.AsTensor<int>().ToArray();
      return true;
    }
    catch
    {
      result = null;
      return false;
    }
  }


  private static Tokenizer CreateSentencePieceTokenizerFromJsonPath(string tokenizerJsonPath)
  {
    if (!File.Exists(tokenizerJsonPath))
      throw new FileNotFoundException($"Tokenizer JSON not found at path '{tokenizerJsonPath}'.", tokenizerJsonPath);

    var directory = Path.GetDirectoryName(tokenizerJsonPath) ?? throw new InvalidOperationException("Tokenizer directory not found.");
    var sentencePiecePath = Path.Combine(directory, "sentencepiece.bpe.model");

    if (!File.Exists(sentencePiecePath))
      throw new FileNotFoundException(
        "Tokenizer JSON support requires the sidecar sentencepiece model file 'sentencepiece.bpe.model' in the same directory.",
        sentencePiecePath);

    using var stream = File.OpenRead(sentencePiecePath);
    return SentencePieceTokenizer.Create(stream, addBeginningOfSentence: true, addEndOfSentence: true, specialTokens: null);
  }

  private float[][] RunModelBatch(IReadOnlyList<long[]> sequences)
  {
    var batchSize = sequences.Count;
    var maxLength = 0;
    foreach (var tokens in sequences)
      maxLength = Math.Max(maxLength, tokens.Length);

    // Padding a destra con id 0; l'attention mask esclude il padding dal calcolo.
    var inputIdsTensor = new DenseTensor<long>([batchSize, maxLength]);
    var attentionMaskTensor = new DenseTensor<long>([batchSize, maxLength]);

    for (var row = 0; row < batchSize; row++)
    {
      var tokens = sequences[row];
      for (var col = 0; col < tokens.Length; col++)
      {
        inputIdsTensor[row, col] = tokens[col];
        attentionMaskTensor[row, col] = 1;
      }
    }

    var modelInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
      NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
    };

    if (_requiresTokenTypeIds)
    {
      var tokenTypeIdsTensor = new DenseTensor<long>([batchSize, maxLength]);
      tokenTypeIdsTensor.Fill(0);
      modelInputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
    }

    var tokenCounts = new int[batchSize];
    for (var row = 0; row < batchSize; row++)
      tokenCounts[row] = sequences[row].Length;

    using var modelResults = _modelSession.Run(modelInputs);
    var results = modelResults.ToList();
    return ExtractEmbeddingVectors(results, tokenCounts);
  }

  private float[][] ExtractEmbeddingVectors(IReadOnlyList<DisposableNamedOnnxValue> results, int[] tokenCounts)
  {
    var batchSize = tokenCounts.Length;

    foreach (var result in results)
    {
      if (!string.Equals(result.Name, "sentence_embedding", StringComparison.OrdinalIgnoreCase))
        continue;

      if (TryExtractRank2Rows(result, batchSize, out var sentenceEmbeddings))
        return sentenceEmbeddings;
    }

    foreach (var result in results)
    {
      if (TryExtractRank2Rows(result, batchSize, out var pooled))
        return pooled;
    }

    foreach (var result in results)
    {
      if (TryExtractRank3MeanPooledRows(result, tokenCounts, out var meanPooled))
        return meanPooled;
    }

    // Last-resort fallback (unknown output layout): only safe without batching,
    // because it cannot be attributed to individual rows.
    if (batchSize == 1)
    {
      foreach (var result in results.Reverse())
      {
        try
        {
          return [result.AsTensor<float>().ToArray()];
        }
        catch
        {
        }
      }
    }

    _logger?.LogWarning("No float tensor output found in ONNX model results.");
    var empty = new float[batchSize][];
    for (var i = 0; i < batchSize; i++)
      empty[i] = Array.Empty<float>();
    return empty;
  }

  private static bool TryExtractRank2Rows(DisposableNamedOnnxValue output, int batchSize, out float[][] rows)
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
        rows[row] = new float[hiddenSize];
        Array.Copy(values, row * hiddenSize, rows[row], 0, hiddenSize);
      }

      return true;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryExtractRank3MeanPooledRows(DisposableNamedOnnxValue output, int[] tokenCounts, out float[][] rows)
  {
    rows = null;

    try
    {
      var tensor = output.AsTensor<float>();
      var dims = tensor.Dimensions;
      if (dims.Length != 3 || dims[0] != tokenCounts.Length || dims[1] <= 0 || dims[2] <= 0)
        return false;

      var sequenceLength = (int)dims[1];
      var hiddenSize = (int)dims[2];
      var values = tensor.ToArray();
      rows = new float[tokenCounts.Length][];

      for (var row = 0; row < tokenCounts.Length; row++)
      {
        var effectiveTokens = Math.Clamp(tokenCounts[row], 1, sequenceLength);
        var baseOffset = row * sequenceLength * hiddenSize;
        var pooled = new float[hiddenSize];

        for (var token = 0; token < effectiveTokens; token++)
        {
          var offset = baseOffset + token * hiddenSize;
          for (var i = 0; i < hiddenSize; i++)
            pooled[i] += values[offset + i];
        }

        var divisor = 1f / effectiveTokens;
        for (var i = 0; i < hiddenSize; i++)
          pooled[i] *= divisor;

        rows[row] = pooled;
      }

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
    _tokenizerSession?.Dispose();
    _modelSession.Dispose();
  }
}