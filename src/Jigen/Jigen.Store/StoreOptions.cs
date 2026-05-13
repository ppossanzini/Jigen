using Jigen.Indexers;

namespace Jigen;

public class StoreOptions
{
  public string DataBasePath { get; set; }
  public string DataBaseName { get; set; }
  public const string ContentSuffix = "content";
  public const string EmbeddingSuffix = "vectors";

  public IIndexer Indexer = new BruteForceIndexer();
}