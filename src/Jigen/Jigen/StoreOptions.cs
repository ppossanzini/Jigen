namespace Jigen;

public class StoreOptions
{
  public string DataBasePath { get; set; }
  public string DataBaseName { get; set; }
  public string ContentSuffix { get; set; } = "content";
  public string EmbeddingSuffix { get; set; } = "vectors";

  public int InitialContentDBSize { get; set; } = 200;
  public int InitialVectorDBSize { get; set; } = 200;
  public int IncrementStepPercentage { get; set; } = 10;
  public int FreeSpaceLimitPercentage { get; set; } = 2;
  public int VectorSize { get; set; } = 1536;

  // public Func<T[], TE[]> QuantizationFunction { get; set; } = (i) => throw new InvalidOperationException("Quantization Function not provided");
}