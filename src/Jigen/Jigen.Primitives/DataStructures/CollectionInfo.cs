namespace Jigen.DataStructures;

public class CollectionInfo
{
  public string Name { get; set; }
  public int Vectors { get; set; }
  public int Dimensions { get; set; }
  public long ContentSize { get; set; }
  public long VectorSize { get; set; }

  /// <summary>Vector-index metrics; null when the indexer does not expose them.</summary>
  public CollectionIndexInfo Index { get; set; }
}