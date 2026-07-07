namespace Jigen.DataStructures;

/// <summary>Structure/size metrics of a collection's vector index (HNSW).</summary>
public class CollectionIndexInfo
{
  /// <summary>On-disk size of the index files (.hnsw.vec + .hnsw.adj, plus legacy .hnsw if present).</summary>
  public long IndexSizeBytes { get; set; }

  /// <summary>Live (non-deleted) graph nodes.</summary>
  public int Nodes { get; set; }

  /// <summary>Soft-deleted graph nodes still present in the graph.</summary>
  public int DeletedNodes { get; set; }

  /// <summary>Highest HNSW layer in the graph.</summary>
  public int MaxLevel { get; set; }

  /// <summary>NodesPerLevel[L] = live nodes whose top layer is exactly L.</summary>
  public int[] NodesPerLevel { get; set; } = [];

  /// <summary>Average layer-0 degree of live nodes.</summary>
  public double AverageDegree { get; set; }

  /// <summary>"None" or "SQ8".</summary>
  public string Quantization { get; set; } = "None";
}
