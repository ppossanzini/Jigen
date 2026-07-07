using Jigen.Indexers;

namespace Jigen;

public class StoreOptions
{
  public string DataBasePath { get; set; }
  public string DataBaseName { get; set; }
  public const string ContentSuffix = "content";
  public const string EmbeddingSuffix = "vectors";

  public IIndexer Indexer = new BruteForceIndexer();

  /// <summary>
  /// When true, SaveChangesAsync triggers ShrinkAsync automatically once both
  /// shrink thresholds below are exceeded.
  /// </summary>
  public bool AutoShrink { get; set; } = false;

  /// <summary>Minimum dead bytes (deletes + overwrites) before a shrink is worthwhile.</summary>
  public long ShrinkMinDeadBytes { get; set; } = 64L * 1024 * 1024;

  /// <summary>Minimum dead/total ratio of the data files before a shrink is worthwhile.</summary>
  public double ShrinkFragmentationThreshold { get; set; } = 0.4;

  /// <summary>
  /// Background indexing workers. Entries are distributed round-robin and the
  /// HNSW graph supports concurrent inserts, so a single collection scales
  /// across all workers. Even one worker pipelines graph construction off the
  /// writer thread.
  /// </summary>
  public int IndexerWorkers { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

  /// <summary>
  /// When true (default), opening a database that was not closed cleanly
  /// (crash, kill) reconciles the vector index with the store content,
  /// restoring index entries whose updates were lost. See <see cref="Store.ReconcileIndexAsync"/>.
  /// </summary>
  public bool ReconcileOnUncleanShutdown { get; set; } = true;
}