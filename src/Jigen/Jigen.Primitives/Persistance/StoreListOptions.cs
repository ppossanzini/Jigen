namespace Jigen.Persistance;

public class StoreListOptions
{
  public string FilePath { get; set; }
  public bool FixedSizeItems { get; set; }
  public int MaxPageSize { get; set; } = 1024 * 1024;
}