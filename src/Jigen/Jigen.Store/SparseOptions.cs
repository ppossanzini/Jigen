namespace Jigen;

/// <summary>
/// Sparse index write policy. Set via <see cref="StoreOptions.Sparse"/>.
/// <para>
/// Each commit writes one immutable segment, so committing every ingestion
/// batch produces thousands of tiny segments during a bulk load. Documents are
/// buffered instead, and a background worker merges the segments back when a
/// collection reads too many of them.
/// </para>
/// </summary>
public class SparseOptions
{
  /// <summary>
  /// Distinct terms the buffer must hold before it is written as a segment.
  /// Default 1000. Values below 1 commit every batch.
  /// </summary>
  public int CommitMinTerms { get; set; } = 1000;

  /// <summary>
  /// Maximum time since the last commit of a collection: past it, the buffered
  /// documents are written even when <see cref="CommitMinTerms"/> is not
  /// reached. Default 60s. Buffered documents are not searchable yet.
  /// </summary>
  public TimeSpan CommitMaxDelay { get; set; } = TimeSpan.FromSeconds(60);

  /// <summary>
  /// A collection reading more segments than this is compacted into one by the
  /// background worker. Default 10. Values below 1 disable the automatic
  /// compaction; <see cref="Store.ShrinkAsync"/> still merges the segments.
  /// </summary>
  public int MaxSegments { get; set; } = 10;

  /// <summary>
  /// Interval of the background worker that commits the expired buffers and
  /// compacts the collections over <see cref="MaxSegments"/>. Default 5s.
  /// Zero or negative disables the worker: buffers are then committed only by
  /// ingestion, WAL checkpoints, <see cref="Store.FlushSparseIndexes"/>,
  /// shrink and close.
  /// </summary>
  public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(5);
}
