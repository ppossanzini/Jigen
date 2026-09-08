using Jigen.SemanticTools;

namespace Jigen.Embedding.Handlers;

public class EmbeddingSettings
{
  public string DefaultModel { get; set; } = "default";

  /// <summary>
  /// Named text embedding models. When empty, the legacy TokenizerPath and
  /// EmbeddingsModelPath settings are exposed as the "default" model.
  /// </summary>
  public Dictionary<string, TextEmbeddingModelSettings> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

  public string TokenizerPath { get; set; }
  public string EmbeddingsModelPath { get; set; }

  public EmbeddingGeneratorOptions GeneratorOptions { get; set; } = new EmbeddingGeneratorOptions();

  public int EmbeddingsMaxConcurrency { get; set; } = 2;
  public int EmbeddingsQueueCapacity { get; set; } = 256;
  public int EmbeddingsQueueTimeoutSeconds { get; set; } = 60;
  
  public string DefaultTask { get; set; }

  /// <summary>
  /// Path to the ONNX vision model (e.g. nomic-embed-vision-v1.5). When empty
  /// or whitespace, image embeddings are not configured and image requests fail
  /// with a clear configuration error instead of crashing the server.
  /// </summary>
  public string ImagesModelPath { get; set; }

  public ImageEmbeddingGeneratorOptions ImageGeneratorOptions { get; set; } = new ImageEmbeddingGeneratorOptions();

  public int ImageEmbeddingsMaxConcurrency { get; set; } = 2;
  public int ImageEmbeddingsQueueCapacity { get; set; } = 256;
  public int ImageEmbeddingsQueueTimeoutSeconds { get; set; } = 60;
}

public class TextEmbeddingModelSettings
{
  public string TokenizerPath { get; set; }
  public string ModelPath { get; set; }
  public EmbeddingGeneratorOptions GeneratorOptions { get; set; } = new();
  public int MaxConcurrency { get; set; } = 2;
  public int QueueCapacity { get; set; } = 256;
  public int QueueTimeoutSeconds { get; set; } = 60;
  public string DefaultTask { get; set; }
}
