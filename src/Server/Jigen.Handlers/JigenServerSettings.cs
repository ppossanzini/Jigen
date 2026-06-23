using Jigen.SemanticTools;

namespace Jigen.Handlers;

public class JigenServerSettings
{
  public string DataFolderPath { get; set; }
  
  public string TokenizerPath { get; set; }
  public string EmbeddingsModelPath { get; set; }
  
  public EmbeddingGeneratorOptions EmbeddingGeneratorOptions { get; set; } = new EmbeddingGeneratorOptions();
  
  public int EmbeddingsMaxConcurrency { get; set; } = 2;
  public int EmbeddingsQueueCapacity { get; set; } = 256;
  public int EmbeddingsQueueTimeoutSeconds { get; set; } = 60;

  // ReSharper disable once InconsistentNaming
  public  int MemoryLimitMB { get; set; } = 2048;
}