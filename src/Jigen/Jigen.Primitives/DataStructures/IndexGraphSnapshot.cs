namespace Jigen.DataStructures;

public class IndexGraphNode
{
  public int PositionId { get; set; }

  /// <summary>Base64 of the raw vector key bytes.</summary>
  public string Key { get; set; }

  public int MaxLevel { get; set; }
  public bool IsDeleted { get; set; }

  /// <summary>Neighbor count at layer 0 (or at the requested layer when a level filter is set).</summary>
  public int Degree { get; set; }

  /// <summary>PCA coordinates, length == snapshot.Dimensions, each component normalized to [-1, 1].</summary>
  public float[] Position { get; set; } = [];
}

public class IndexGraphEdge
{
  public int Source { get; set; } // PositionId
  public int Target { get; set; } // PositionId
  public int Level { get; set; }  // HNSW layer this edge belongs to
}

public class IndexGraphSnapshot
{
  public string Collection { get; set; }
  public int Dimensions { get; set; }          // 2 or 3
  public int TotalNodes { get; set; }          // graph slots excluding slot 0
  public int LiveNodes { get; set; }
  public int DeletedNodes { get; set; }
  public int ReturnedNodes { get; set; }
  public int MaxLevel { get; set; }
  public int EntrypointPositionId { get; set; }
  public bool Truncated { get; set; }          // true when limit cut the sample
  public IList<IndexGraphNode> Nodes { get; set; } = [];
  public IList<IndexGraphEdge> Edges { get; set; } = [];

  /// <summary>PCA coordinates of the optional query vector, projected in the SAME PCA
  /// basis as <see cref="Nodes"/> (computed by appending it to the sampled batch before
  /// projection). Null when no query vector was supplied to GetGraphSnapshot.</summary>
  public float[] QueryPosition { get; set; }
}
