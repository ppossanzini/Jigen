using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Extensions;

public static class SearchExtensions
{
  public static IEnumerable<(VectorEntry entry, float score)> Search(this Store store, string collection, float[] queryVector, int top, IFilterExpression contentFilter = null)
  {
    return store.Options.Indexer.Search(store, collection, queryVector, top, contentFilter);
  }

  /// <summary>
  /// Search with a per-query beam width override (see <see cref="IIndexer"/>);
  /// efSearch 0 or negative uses the index default.
  /// </summary>
  public static IEnumerable<(VectorEntry entry, float score)> Search(this Store store, string collection, float[] queryVector, int top, int efSearch, IFilterExpression contentFilter = null)
  {
    return store.Options.Indexer.Search(store, collection, queryVector, top, efSearch, contentFilter);
  }

  public static IEnumerable<VectorEntry> Search(this Store store, string collection, IFilterExpression contentFilter = null)
  {
    return store.Options.Indexer.Search(store, collection, contentFilter);
  }
}