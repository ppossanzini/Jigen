using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Extensions;

public static class SearchExtensions
{
  public static IEnumerable<(VectorEntry entry, float score)> Search(this Store store, string collection, float[] queryVector, int top, IFilterExpression contentFilter = null)
  {
    return store.Options.Indexer.Search(store, collection, queryVector, top, contentFilter);
  }

  public static IEnumerable<VectorEntry> Search(this Store store, string collection, IFilterExpression contentFilter = null)
  {
    return store.Options.Indexer.Search(store, collection, contentFilter);
  }
}