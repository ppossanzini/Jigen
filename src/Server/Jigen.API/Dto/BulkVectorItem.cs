namespace Jigen.API.Dto;

public class BulkVectorItem
{
  public string Key { get; set; }
  public string KeyType { get; set; }
  public object Payload { get; set; }

  /// <summary>Every vector of the entry, all with the collection dimensions. A single-vector entry is an array of one.</summary>
  public float[][] Vectors { get; set; }

  /// <summary>
  /// Sparse (lexical) terms of each vector, aligned by index with
  /// <see cref="Vectors"/>: one object mapping term id to weight per vector.
  /// </summary>
  public Dictionary<int, float>[] Sparse { get; set; }
}
