// <copyright file="TravelingCosts.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

// ConcurrentDictionary replaced with plain Dictionary:
// TravelingCosts is scoped per-operation (one per query node, one per insert node)
// and is never shared across threads, so ConcurrentDictionary overhead is wasted.

using System.Runtime.CompilerServices;

namespace Jigen.Indexer
{
  /// <summary>
  /// Calculates and caches distances from arbitrary nodes to a fixed destination node.
  /// Implements <see cref="IComparer{T}"/> so it can drive heap ordering directly.
  /// </summary>
  public class TravelingCosts(IndexNode destination, SmallWorldOptions options) : IComparer<IndexNode>
  {
    private static readonly Comparer<float> DistanceComparer = Comparer<float>.Default;

    // Plain Dictionary: per-operation, single-threaded access only.
    // ~3-5x faster than ConcurrentDictionary for the hit path.
    // Lazily allocated: graph nodes never queried as destination pay nothing,
    // and ClearCache releases the memory once the owning operation completes.
    private Dictionary<int, float> _cache;

    /// <summary>
    /// Returns the distance from <paramref name="departure"/> to the destination.
    /// Results are cached by PositionId for the lifetime of this instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float From(IndexNode departure, bool usecache = true)
    {
      if (!usecache)
        return options.DefaultDistanceFunction(departure, destination);

      // Snapshot the field: a concurrent ClearCache (another insert releasing
      // this node) must not null it between the ??= and the use. Mutations are
      // serialized by the owning node's lock; this only guards the swap.
      var cache = _cache ??= new Dictionary<int, float>();
      if (cache.TryGetValue(departure.PositionId, out var cached))
        return cached;

      var result = options.DefaultDistanceFunction(departure, destination);
      cache[departure.PositionId] = result;
      return result;
    }

    /// <summary>
    /// Drops the cached distances. Called when the operation that was filling the
    /// cache completes (e.g. a node insert): graph nodes live for the lifetime of
    /// the index and would otherwise retain hundreds of stale entries each.
    /// </summary>
    public void ClearCache() => _cache = null;

    /// <summary>
    /// Compares two nodes by their distance to the destination (closer = smaller).
    /// </summary>
    public int Compare(IndexNode x, IndexNode y)
      => DistanceComparer.Compare(From(x), From(y));
  }
}
