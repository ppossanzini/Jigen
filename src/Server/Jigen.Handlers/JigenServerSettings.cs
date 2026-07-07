
namespace Jigen.Handlers;

public class JigenServerSettings
{
  public string DataFolderPath { get; set; }
  // ReSharper disable once InconsistentNaming
  public  int MemoryLimitMB { get; set; } = 2048;

  /// <summary>
  /// Seconds between durability checkpoints: every open database gets a
  /// SaveChangesAsync (fsync of content/embeddings/index, graph flush), which
  /// also surfaces background ingestion errors in the logs. Deletes and
  /// appends are group-committed: this bounds the data-loss window on an
  /// OS/hardware crash. 0 disables the periodic checkpoint.
  /// </summary>
  public int CheckpointIntervalSeconds { get; set; } = 30;
}