
namespace Jigen.Handlers;

public class JigenServerSettings
{
  public string DataFolderPath { get; set; }
  // ReSharper disable once InconsistentNaming
  public  int MemoryLimitMB { get; set; } = 2048;
}