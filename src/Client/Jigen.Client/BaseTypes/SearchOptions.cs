namespace Jigen.Client.BaseTypes;

/// <summary>
/// Optional per-query search tuning. Defaults keep the server's configured
/// behavior.
/// </summary>
public sealed class SearchOptions
{
  /// <summary>
  /// HNSW beam width override for this query (recall vs latency); 0 uses the
  /// server default. Ignored by exact (brute force) indexes.
  /// </summary>
  public int EfSearch { get; set; }

  /// <summary>
  /// Return keys and scores only: results come back with a null Content,
  /// skipping the content bytes on the wire.
  /// </summary>
  public bool NoContent { get; set; }

  /// <summary>Drop results scoring below this similarity.</summary>
  public float? MinScore { get; set; }

  /// <summary>Drop dense hits below this similarity before the hybrid fusion.</summary>
  public float? MinDenseScore { get; set; }

  /// <summary>Drop sparse hits below this score before the hybrid fusion.</summary>
  public float? MinSparseScore { get; set; }
}
