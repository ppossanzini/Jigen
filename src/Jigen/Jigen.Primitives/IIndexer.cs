using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen;

public interface IIndexer
{
  void AddToIndex(VectorEntry entry, bool waitForIndexing = false);
  void RemoveFromIndex(string collection, byte[] key);

  IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top, IFilterExpression contentFilter = null);
  IEnumerable<VectorEntry> Search(IStore store, string collection, IFilterExpression contentFilter = null);

  /// <summary>
  /// Search with a per-query beam width override (HNSW efSearch: recall vs
  /// latency). Indexers without a beam (exact scans) ignore it; 0 or negative
  /// means "use the configured default".
  /// </summary>
  IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top, int efSearch, IFilterExpression contentFilter = null)
    => Search(store, collection, queryVector, top, contentFilter);

  Task FlushAsync() => Task.CompletedTask;
  Task ShrinkAsync() => Task.CompletedTask;

  /// <summary>
  /// Flushes and releases every resource held by the index (file handles,
  /// background flush loops). Called by the owning store on Close.
  /// </summary>
  Task CloseAsync() => Task.CompletedTask;

  /// <summary>
  /// Aligns the index with the store content after a possible divergence
  /// (e.g. index updates lost in a crash): re-indexes store entries missing
  /// from the index and drops indexed entries no longer in the store.
  /// No-op for indexers that read the store directly.
  /// </summary>
  Task ReconcileAsync(IStore store) => Task.CompletedTask;
}