using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen.Extensions;

public static class SearchExtensions
{
  /// <summary>
  /// Returns metadata groups ranked by the highest observed embedding score.
  /// The entry is the winning embedding; metadataIndex is its stable metadata ID.
  /// candidateBudget caps the sum of candidate counts requested across attempts,
  /// including repeated candidates. Fewer than top groups may be returned.
  /// Approximate indexes do not guarantee exact grouped top-k results.
  /// </summary>
  public static IEnumerable<(int metadataIndex, VectorEntry entry, float score)> SearchByMetadata(
    this Store store, string collection, float[] queryVector, int top, int candidateBudget,
    int efSearch = 0, IFilterExpression contentFilter = null)
  {
    if (top <= 0 || candidateBudget <= 0) return [];

    var groups = new Dictionary<int, (VectorEntry entry, float score)>();
    var remaining = candidateBudget;
    var requested = Math.Min(top, remaining);
    while (remaining > 0)
    {
      requested = Math.Min(requested, remaining);
      var observed = 0;
      foreach (var candidate in store.Options.Indexer.Search(
        store, collection, queryVector, requested, efSearch, contentFilter).Take(requested))
      {
        observed++;
        if (!store.SharedMetadata.TryGetMetadataIndex(collection, candidate.entry.Id, out var metadataIndex)
          || float.IsNaN(candidate.score))
          continue;

        if (!groups.TryGetValue(metadataIndex, out var current) || candidate.score > current.score)
          groups[metadataIndex] = candidate;
      }

      remaining -= requested;
      if (groups.Count >= top || observed == 0) break;
      requested = (int)Math.Min((long)requested * 2, remaining);
    }

    return groups.OrderByDescending(group => group.Value.score)
      .ThenBy(group => group.Key)
      .Take(top)
      .Select(group => (group.Key, group.Value.entry, group.Value.score))
      .ToArray();
  }

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