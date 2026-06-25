using Jigen.SemanticTools;

namespace Jigen.TextEmbedding.Handlers;

public class EmbeddingSettings
{
  public string TokenizerPath { get; set; }
  public string EmbeddingsModelPath { get; set; }

  public EmbeddingGeneratorOptions GeneratorOptions { get; set; } = new EmbeddingGeneratorOptions();

  public int EmbeddingsMaxConcurrency { get; set; } = 2;
  public int EmbeddingsQueueCapacity { get; set; } = 256;
  public int EmbeddingsQueueTimeoutSeconds { get; set; } = 60;
  
  public string DefaultTask { get; set; }
}