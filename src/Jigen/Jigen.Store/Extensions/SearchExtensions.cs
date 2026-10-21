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

  /// <summary>
  /// Hybrid search: runs the dense and the lexical search separately and fuses
  /// their rankings by position, so the two score scales are never compared.
  /// A null query or a zero weight leaves that modality out; both at zero is
  /// rejected. Each modality is searched deeper than the final cut, because a
  /// result ranked just outside one list can still win the fusion.
  /// </summary>
  public static IEnumerable<(VectorEntry entry, double score, bool inDense, bool inSparse,
      float denseScore, float sparseScore)>
    SearchHybrid(this Store store, string collection, float[] denseQuery, SparseVector sparseQuery,
      int top, float denseWeight = 1f, float sparseWeight = 1f, IFilterExpression contentFilter = null)
  {
    if (top <= 0) return [];
    if (denseQuery is null && sparseQuery is null)
      throw new ArgumentException("A hybrid search needs at least one of the two vectors.");

    var depth = (int)Math.Min(int.MaxValue, Math.Max(top, (long)top * 4));

    var dense = denseWeight <= 0 || denseQuery is null
      ? []
      : store.Search(collection, denseQuery, depth, contentFilter).ToArray();

    var sparse = sparseWeight <= 0 || sparseQuery is null
      ? []
      : store.SearchSparse(collection, sparseQuery, depth, contentFilter).ToArray();

    var fused = RankFusion.Fuse(
      dense.Select(item => item.entry.Id).ToArray(),
      sparse.Select(item => item.key).ToArray(),
      denseWeight,
      sparseWeight,
      top);

    var contents = new Dictionary<byte[], byte[]>(ByteArrayEqualityComparer.Instance);
    foreach (var item in dense) contents[item.entry.Id] = item.entry.Content.ToArray();
    foreach (var item in sparse) contents[item.key] = item.content;

    // Original scores, kept for the caller that must show why an entry won:
    // position decided the order, the modality scores explain the position.
    var denseScores = new Dictionary<byte[], float>(ByteArrayEqualityComparer.Instance);
    foreach (var item in dense) denseScores[item.entry.Id] = item.score;
    var sparseScores = new Dictionary<byte[], float>(ByteArrayEqualityComparer.Instance);
    foreach (var item in sparse) sparseScores[item.key] = item.score;

    return fused
      .Where(rank => contents.ContainsKey(rank.Key))
      .Select(rank => (new VectorEntry
      {
        Id = rank.Key,
        CollectionName = collection,
        Content = contents[rank.Key]
      }, rank.Score, rank.InDense, rank.InSparse,
        denseScores.TryGetValue(rank.Key, out var denseScore) ? denseScore : 0f,
        sparseScores.TryGetValue(rank.Key, out var sparseScore) ? sparseScore : 0f))
      .ToArray();
  }

  /// <summary>
  /// Exact lexical search: scores every entry that shares at least one term with
  /// the query, by summing the products of the shared weights. The result is the
  /// true top-k, not an approximation; when a filter is present the whole
  /// candidate set is evaluated, because scores alone cannot decide the cut.
  /// </summary>
  public static IEnumerable<(byte[] key, byte[] content, float score)> SearchSparse(
    this Store store, string collection, SparseVector query, int top,
    IFilterExpression contentFilter = null)
  {
    if (query is null || top <= 0) return [];

    var sparseIndex = store.Options.SparseIndex;
    if (sparseIndex is null) return [];

    store.EnsureSparseIndexBuilt(collection);

    // Without a filter the index can cut at top; with one, every candidate must
    // be tested before the cut, so the whole ranking is requested.
    var ranked = sparseIndex.Search(collection, query, contentFilter is null ? top : int.MaxValue);

    var results = new List<(byte[], byte[], float)>(Math.Min(top, ranked.Count));
    foreach (var (key, score) in ranked)
    {
      if (results.Count >= top) break;

      var content = store.GetContent(collection, key);
      if (content is null) continue;

      if (contentFilter is not null && !MessagePackFilterEvaluator.Matches(content, contentFilter))
        continue;

      results.Add((key, content, score));
    }

    return results;
  }

  /// <summary>
  /// Lexical search grouped by shared metadata record: at most <paramref name="top"/>
  /// groups, each represented by its winning entry and the highest score seen.
  /// <paramref name="candidateBudget"/> caps the entries requested from the
  /// index, so a group-rich collection cannot turn one query into a full scan.
  /// </summary>
  public static IEnumerable<(int metadataIndex, byte[] key, byte[] content, float score)>
    SearchSparseByMetadata(this Store store, string collection, SparseVector query, int top,
      int candidateBudget, IFilterExpression contentFilter = null)
  {
    if (query is null || top <= 0 || candidateBudget <= 0) return [];

    var sparseIndex = store.Options.SparseIndex;
    if (sparseIndex is null) return [];

    store.EnsureSparseIndexBuilt(collection);

    var ranked = sparseIndex.Search(collection, query, candidateBudget);
    var groups = new Dictionary<int, (byte[] key, byte[] content, float score)>();

    foreach (var (key, score) in ranked)
    {
      if (groups.Count >= top) break;

      if (!store.SharedMetadata.TryGetMetadataIndex(collection, key, out var metadataIndex))
        continue;

      if (groups.TryGetValue(metadataIndex, out var current) && current.score >= score)
        continue;

      var content = store.GetContent(collection, key);
      if (content is null) continue;

      if (contentFilter is not null && !MessagePackFilterEvaluator.Matches(content, contentFilter))
        continue;

      groups[metadataIndex] = (key, content, score);
    }

    return groups
      .OrderByDescending(group => group.Value.score)
      .ThenBy(group => group.Key)
      .Take(top)
      .Select(group => (group.Key, group.Value.key, group.Value.content, group.Value.score))
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