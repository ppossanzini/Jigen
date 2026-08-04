namespace Jigen;

/// <summary>
/// WAL durability policy.
/// </summary>
public enum WalDurability
{
  /// <summary>No fsync on WAL writes. Durability only at checkpoint time.</summary>
  None,

  /// <summary>
  /// Periodic fsync every <see cref="WalOptions.MaxGroupDelay"/> or
  /// <see cref="WalOptions.MaxGroupBatchCount"/> writes. Balances latency and throughput.
  /// </summary>
  Group,

  /// <summary>fsync after every WAL write. Maximum durability, higher I/O overhead.</summary>
  PerWrite,
}

/// <summary>
/// Configuration for the Write-Ahead Log. Set via <see cref="StoreOptions.Wal"/>.
/// When <see cref="Enabled"/> is true, all writes go to the WAL file
/// ({name}.wal.jigen) before they are enqueued for background writing to the
/// data files.
/// </summary>
public class WalOptions
{
  /// <summary>Enable the WAL. Default false (no WAL file is created).</summary>
  public bool Enabled { get; set; }

  /// <summary>fsync policy. Default <see cref="WalDurability.Group"/>.</summary>
  public WalDurability Durability { get; set; } = WalDurability.Group;

  /// <summary>Maximum time between fsync calls in Group mode. Default 10ms.</summary>
  public TimeSpan MaxGroupDelay { get; set; } = TimeSpan.FromMilliseconds(10);

  /// <summary>Maximum batch count before a forced fsync in Group mode. Default 8.</summary>
  public int MaxGroupBatchCount { get; set; } = 8;

  /// <summary>Interval between automatic checkpoint passes. Default 30s.</summary>
  public TimeSpan CheckpointInterval { get; set; } = TimeSpan.FromSeconds(30);
}
