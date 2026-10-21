using System.Collections.Concurrent;
using Jigen.Indexer;
using Jigen.Indexers;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.Model;

public class DatabasesManager
{
  private readonly SystemDB _master;
  private readonly JigenServerSettings _settings;

  public DatabasesManager(SystemDB master, IOptions<JigenServerSettings> settings)
  {
    this._master = master;
    this._settings = settings.Value;
    Init();
  }

  // Concurrent: request handlers read it while CreateDatabase/DeleteDatabase
  // mutate it, under a parallel host.
  public ConcurrentDictionary<string, Store> ActiveDatabases { get; init; } = new();

  /// <summary>
  /// Single factory for database stores: creation (CreateDatabase) and reopen
  /// (Init) MUST use the same options, or a freshly created database would run
  /// on a different indexer until the next restart.
  /// </summary>
  /// <summary>
  /// Per-database graph folder: with a shared one, same-named collections of
  /// different databases would map to the SAME graph files.
  /// </summary>
  public string GraphFolderFor(string name) => Path.Combine(_settings.DataFolderPath, "hnsw", name);

  public Store OpenStore(string name)
  {
    var hnswPath = GraphFolderFor(name);
    var graphIsNew = !Directory.Exists(hnswPath);

    var index = _settings.Index ?? new JigenIndexSettings();

    SmallWorldIndexer CreateHnsw() => new(
      new(m: index.M, efConstruction: index.EfConstruction, efSearch: index.EfSearch, storagePath: hnswPath)
      {
        Quantization = index.Sq8Quantization ? VectorQuantization.SQ8 : VectorQuantization.None,
        ExactRerank = index.ExactRerank
      });

    IIndexer indexer = CreateHnsw();

    // When a lazy threshold is configured, wrap the HNSW indexer so graph
    // construction is deferred until the total vector count crosses it.
    // Ingestion below the threshold is pure file writes (no graph overhead).
    if (index.LazyHnswThreshold > 0)
      indexer = new LazyIndexer(CreateHnsw, index.LazyHnswThreshold);

    var storeOptions = new StoreOptions
    {
      DataBaseName = name,
      DataBasePath = _settings.DataFolderPath,
      ReconcileOnUncleanShutdown = _settings.ReconcileOnUncleanShutdown,
      Indexer = indexer,
      Sparse = new SparseOptions { MaxTerms = index.SparseMaxTerms },
      // Null disables sparse search: the slot is still stored, it is just not
      // indexed. One instance per database, since collections live there.
      SparseIndex = index.SparseEnabled ? new SparseIndex() : null,
      Wal = BuildWalOptions()
    };

    if (_settings.IndexerWorkers > 0)
      storeOptions.IndexerWorkers = _settings.IndexerWorkers;

    var store = new Store(storeOptions);

    // One-time rebuild for databases whose graph does not exist yet while the
    // store has content: graphs from the legacy SHARED hnsw folder, or
    // databases historically created without an indexer.
    if (graphIsNew && store.GetCollections().Any())
      store.ReconcileIndexAsync().GetAwaiter().GetResult();

    return store;
  }

  /// <summary>
  /// The log is written before the data files, and a sparse vector is refused
  /// without it, so every database of this server gets the configured policy.
  /// </summary>
  private WalOptions BuildWalOptions()
  {
    var wal = _settings.Wal ?? new JigenWalSettings();
    return new WalOptions
    {
      Enabled = wal.Enabled,
      Durability = wal.Durability,
      MaxGroupDelay = TimeSpan.FromMilliseconds(Math.Max(0, wal.MaxGroupDelayMs)),
      MaxGroupBatchCount = Math.Max(1, wal.MaxGroupBatchCount),
      CheckpointInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.CheckpointIntervalSeconds))
    };
  }

  public void Init()
  {
    var dbs = _master.System[SystemDB.BASEINFO].Databases;
    foreach (var db in dbs)
    {
      if (ActiveDatabases.ContainsKey(db))
        continue;
      ActiveDatabases.TryAdd(db, OpenStore(db));
    }
  }
}
