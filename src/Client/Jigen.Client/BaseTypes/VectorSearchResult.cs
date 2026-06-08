namespace Jigen.Client.BaseTypes;

public class VectorSearchResult<T>
  where T : class, new()
{
  public VectorKey Key { get; set; }
  public T Content { get; set; }
  public float Score { get; set; }
}
