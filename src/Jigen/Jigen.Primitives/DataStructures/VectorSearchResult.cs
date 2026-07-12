namespace Jigen.DataStructures;

/// <summary>
/// A typed vector-search hit: the same shape the gRPC client returns, for the
/// in-process collections.
/// </summary>
public class VectorSearchResult<T>
  where T : class, new()
{
  public VectorKey Key { get; set; }
  public T Content { get; set; }
  public float Score { get; set; }
}
