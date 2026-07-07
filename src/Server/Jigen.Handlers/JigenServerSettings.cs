
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

  /// <summary>Background indexing workers per database; 0 = automatic (CPU/2, max 8).</summary>
  public int IndexerWorkers { get; set; } = 0;

  /// <summary>
  /// Reconcile the vector index with the store content when a database was
  /// not closed cleanly (crash recovery). Default true.
  /// </summary>
  public bool ReconcileOnUncleanShutdown { get; set; } = true;

  public JigenIndexSettings Index { get; set; } = new();
}

/// <summary>HNSW index parameters applied to every database.</summary>
public class JigenIndexSettings
{
  /// <summary>Max connections per node per layer (2M on layer 0).</summary>
  public int M { get; set; } = 16;

  /// <summary>Construction beam width (build quality vs ingest speed).</summary>
  public int EfConstruction { get; set; } = 200;

  /// <summary>Search beam width (recall vs latency; raise it on large collections).</summary>
  public int EfSearch { get; set; } = 50;

  /// <summary>
  /// SQ8 quantization of the GRAPH vectors: 4x smaller graph files and less
  /// memory bandwidth; store embeddings stay full precision. Applies to
  /// newly written graph records.
  /// </summary>
  public bool Sq8Quantization { get; set; } = false;

  /// <summary>With SQ8: rescore results with full-precision embeddings (default true).</summary>
  public bool ExactRerank { get; set; } = true;
}