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

  /// <summary>
  /// Drop dense hits below this similarity. It applies to a dense-only search and
  /// to the dense half of a hybrid one, so one property means one thing wherever
  /// the query runs.
  /// </summary>
  public float? MinDenseScore { get; set; }

  /// <summary>Drop sparse hits below this score, on a sparse-only search and on the sparse half of a hybrid one.</summary>
  public float? MinSparseScore { get; set; }
}
