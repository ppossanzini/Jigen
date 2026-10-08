namespace Jigen.Embedding.Core.Options;

public class PassageSplittingOptions
{
  public int DefaultTokenSize { get; set; } = 128;
  public int DefaultOverlapSize { get; set; } = 32;
  public int MaxTokenSize { get; set; } = 512;
  public int MaxOverlapSize { get; set; } = 128;
}
