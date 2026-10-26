namespace Jigen;

/// <summary>
/// Sparse index write policy. Set via <see cref="StoreOptions.Sparse"/>.
/// <para>
/// Each commit writes one immutable segment, so committing every ingestion
/// batch produces thousands of tiny segments during a bulk load. Documents are
/// buffered instead, and a background worker merges small segments by size.
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
  /// Size a segment can reach through background merges: a segment at or above
  /// it is never merged again by the worker. Default 256 MB. Zero or negative
  /// disables the automatic merges; <see cref="Store.ShrinkAsync"/> still
  /// merges every segment into one.
  /// </summary>
  public long MaxSegmentBytes { get; set; } = 256L * 1024 * 1024;

  /// <summary>
  /// Segments below this size count as this size when the worker compares
  /// them, so tiny segments are merged as soon as a second one appears.
  /// Larger segments merge only with peers of similar size. Default 1 MB.
  /// </summary>
  public long MinSegmentBytes { get; set; } = 1024L * 1024;

  /// <summary>
  /// Interval of the background worker that commits the expired buffers and
  /// merges the segments chosen by size. Default 5s.
  /// Zero or negative disables the worker: buffers are then committed only by
  /// ingestion, WAL checkpoints, <see cref="Store.FlushSparseIndexes"/>,
  /// shrink and close.
  /// </summary>
  public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(5);
}
