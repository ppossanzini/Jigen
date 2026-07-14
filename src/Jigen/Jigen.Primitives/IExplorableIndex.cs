using Jigen.DataStructures;

namespace Jigen;

/// <summary>
/// Optional capability of an <see cref="IIndexer"/>: exposes the internal graph
/// for statistics and visualization. BruteForceIndexer does NOT implement it.
/// </summary>
public interface IExplorableIndex
{
  CollectionIndexInfo GetIndexInfo(string collection);
  IndexGraphSnapshot GetGraphSnapshot(string collection, int dimensions = 2, int limit = 2000, int? level = null);
}
