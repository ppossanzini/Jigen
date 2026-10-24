namespace Jigen.DataStructures;

public class StoreHeader
{
  /// <summary>
  /// On-disk format of the vector records. 0 marks an archive written before
  /// sparse vectors existed: its records carry dense vectors only. From 2 on,
  /// every vector record ends with the sparse section of the entry.
  /// </summary>
  public int FormatVersion { get; set; }

  public long EmbeddingCurrentPosition { get; set; }
  public long ContentCurrentPosition { get; set; }
}