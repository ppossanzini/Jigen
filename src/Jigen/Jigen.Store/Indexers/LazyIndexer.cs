using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Indexers;

/// <summary>
/// Defers index construction until the total number of stored vectors
/// across all collections exceeds a threshold.
/// While below the threshold, ingestion is pure file writes (no graph
/// construction) and searches use brute-force scanning. Once the threshold
/// is crossed, the index is built from the store in a single
/// reconciliation pass and all subsequent operations use it.
/// </summary>
public class LazyIndexer : IIndexer, IExplorableIndex
{
  private readonly Func<IIndexer> _indexFactory;
  private readonly int _threshold;

  private IIndexer _active;
  private readonly Lock _switchLock = new();
  private bool _switched;

  /// <param name="indexerFactory">Factory that creates the indexer when the threshold is reached.</param>
  /// <param name="threshold">Minimum total vector count before index construction is triggered.</param>
  public LazyIndexer(Func<IIndexer> indexerFactory, int threshold = 100001)
  {
    _indexFactory = indexerFactory ?? throw new ArgumentNullException(nameof(indexerFactory));
    _threshold = threshold;
    _active = new BruteForceIndexer();
  }

  /// <inheritdoc />
  /// <remarks>
  /// During the brute-force phase this is a no-op: vectors are persisted to
  /// the store files but no graph is built, maximising ingestion throughput.
  /// After the index switch, delegates to the indexer.
  /// </remarks>
  public void AddToIndex(VectorEntry entry, bool waitForIndexing = false)
  {
    if (Volatile.Read(ref _switched) )
      _active.AddToIndex(entry, waitForIndexing);
  }

  /// <inheritdoc />
  public void RemoveFromIndex(string collection, byte[] key)
    => _active.RemoveFromIndex(collection, key);

  /// <inheritdoc />
  public IEnumerable<(VectorEntry entry, float score)> Search(
    IStore store, string collection, float[] queryVector, int top,
    IFilterExpression contentFilter = null)
    => Search(store, collection, queryVector, top, efSearch: 0, contentFilter);

  /// <inheritdoc />
  public IEnumerable<(VectorEntry entry, float score)> Search(
    IStore store, string collection, float[] queryVector, int top,
    int efSearch, IFilterExpression contentFilter = null)
  {
    EnsureSwitched(store);
    return _active.Search(store, collection, queryVector, top, efSearch, contentFilter);
  }

  /// <inheritdoc />
  public IEnumerable<VectorEntry> Search(
    IStore store, string collection, IFilterExpression contentFilter = null)
  {
    EnsureSwitched(store);
    return _active.Search(store, collection, contentFilter);
  }

  /// <inheritdoc />
  public async Task FlushAsync() => await _active.FlushAsync();

  /// <inheritdoc />
  public async Task ShrinkAsync() => await _active.ShrinkAsync();

  /// <inheritdoc />
  public async Task CloseAsync() => await _active.CloseAsync();

  /// <inheritdoc />
  /// <remarks>
  /// On first call captures the store reference. If the total vector count
  /// across all collections meets or exceeds the threshold, the indexer is built and activated. Otherwise stays in brute-force mode.
  /// </remarks>
  public async Task ReconcileAsync(IStore store)
  {
    EnsureSwitched(store);
    await _active.ReconcileAsync(store);
  }

  /// <summary>
  /// Checks whether the total number of stored vectors exceeds the configured
  /// threshold and, if so, builds the index from the store in a single
  /// reconciliation pass. The first invocation that crosses the threshold
  /// pays the one-time graph construction cost; all subsequent calls are
  /// cheap double-checks.
  /// </summary>
  private void EnsureSwitched(IStore store)
  {
    if (Volatile.Read(ref _switched))
      return;

    // Count actual vectors from the store position index (not from AddToIndex
    // calls, because during the brute-force phase those are no-ops).
    var totalVectors = 0;
    foreach (var collection in store.GetCollections())
    {
      if (store.GetCollectionIndexOf(collection, out var index))
        totalVectors += index.Count;
    }

    if (totalVectors < _threshold)
      return;

    lock (_switchLock)
    {
      if (Volatile.Read(ref _switched))
        return;

      var hnsw = _indexFactory();
      // Switch BEFORE reconciliation so new AddToIndex calls arriving during
      // the graph build go to indexer (they'll serialize on the graph lock with
      // ReconcileAsync). ReconcileAsync skips entries already in the graph.
      _active = hnsw;
      Volatile.Write(ref _switched, true);

      hnsw.ReconcileAsync(store).GetAwaiter().GetResult();
    }
  }

  /// <inheritdoc />
  /// <remarks>Delegates to the active indexer if it is explorable; returns null otherwise.</remarks>
  public CollectionIndexInfo GetIndexInfo(string collection)
    => (_active as IExplorableIndex)?.GetIndexInfo(collection);

  /// <inheritdoc />
  /// <remarks>Delegates to the active indexer if it is explorable; returns null otherwise.</remarks>
  public IndexGraphSnapshot GetGraphSnapshot(string collection, int dimensions = 2, int limit = 2000, int? level = null)
    => (_active as IExplorableIndex)?.GetGraphSnapshot(collection, dimensions, limit, level);
}