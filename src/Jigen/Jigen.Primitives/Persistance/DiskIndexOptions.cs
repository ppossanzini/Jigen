namespace Jigen.Persistance;

public sealed class DiskIndexOptions
{
  public required string FilePath { get; init; }
  public int ViewSize { get; init; } = 1024 * 1024;
  public bool Recreate { get; init; } = false;
}