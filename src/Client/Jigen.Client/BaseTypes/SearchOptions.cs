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
}
