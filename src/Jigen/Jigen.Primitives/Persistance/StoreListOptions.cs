namespace Jigen.Persistance;

public class StoreListOptions
{
  public string FilePath { get; set; }
  public TimeSpan? FlushInterval { get; set; } = TimeSpan.FromMinutes(1);
}