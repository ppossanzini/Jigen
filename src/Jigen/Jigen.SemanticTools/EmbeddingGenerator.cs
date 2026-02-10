// This Class is from https://github.com/yuniko-software/tokenizer-to-onnx-model sample

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

  /// <summary>
  /// Initializes a new instance of the OnnxEmbeddingGenerator class.
  /// </summary>
  /// <param name="tokenizerPath">Path to the ONNX tokenizer model.</param>
  /// <param name="modelPath">Path to the ONNX embedding model.</param>
  public OnnxEmbeddingGenerator(string tokenizerPath, string modelPath)
  {
    // Initialize tokenizer session with ONNX Extensions
    var tokenizerOptions = new SessionOptions();
    tokenizerOptions.RegisterOrtExtensions();

    _tokenizerSession = new InferenceSession(tokenizerPath, tokenizerOptions);
    _modelSession = new InferenceSession(modelPath);
  }

  /// <summary>
  /// Generates embedding for the input text.
  /// </summary>
  /// <param name="text">The input text.</param>
  /// <returns>The embedding vector as a float array.</returns>
  public float[] GenerateEmbedding(string text)
  {
    // Create input for tokenizer using CreateFromTensor
    var tokenizerInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor("inputs", new DenseTensor<string>([1])
      {
        [0] = text
      })
    };

    // Run tokenizer
    using var tokenizerResults = _tokenizerSession.Run(tokenizerInputs);
    var tokenizerResultsList = tokenizerResults.ToList();

    // Extract tokens and token_indices (order: tokens, instance_indices, token_indices)
    var tokens = tokenizerResultsList[0].AsTensor<int>().ToArray();
    var tokenIndices = tokenizerResultsList[2].AsTensor<int>().ToArray();

    // Convert to input_ids by sorting tokens based on token_indices
    var tokenPairs = tokens.Zip(tokenIndices, (t, i) => (token: t, index: i)).OrderBy(p => p.index).Select(p => (long)p.token).ToArray();

    // Create input_ids tensor with shape [1, tokenPairs.Length]
    var inputIdsTensor = new DenseTensor<long>(tokenPairs, [1, tokenPairs.Length], false);

    var attentionMaskTensor = new DenseTensor<long>([1, tokenPairs.Length]);
    attentionMaskTensor.Fill(1);

    var modelInputs = new List<NamedOnnxValue>
    {
      NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
      NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
    };

    using var modelResults = _modelSession.Run(modelInputs);
    return modelResults.Last().AsTensor<float>().ToArray();
  }


  public void Dispose()
  {
    _tokenizerSession.Dispose();
    _modelSession.Dispose();
  }
}