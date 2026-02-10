namespace Jigen;

public class StoreOptions
{
  public string DataBasePath { get; set; }
  public string DataBaseName { get; set; }
  public string ContentSuffix { get; set; } = "content";
  public string EmbeddingSuffix { get; set; } = "vectors";
}