// <copyright file="TravelingCosts.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>


using System.Collections.Concurrent;

namespace Jigen.Indexer
{
  /// <summary>
  /// Implementation of distance calculation from an arbitrary point to the given destination.
  /// </summary>
  /// <typeparam name="TItem">Type of the points.</typeparam>
  /// <typeparam name="TDistance">Type of the diatnce.</typeparam>
  public class TravelingCosts(IndexNode destination, SmallWorldOptions options) : IComparer<IndexNode>
  {
    /// <summary>
    /// Default distance comaprer.
    /// </summary>
    private static readonly Comparer<float> DistanceComparer = Comparer<float>.Default;


    /// <summary>
    /// Cached values.
    /// </summary>
    private readonly ConcurrentDictionary<IndexNode, float> _cache = new ();


    /// <summary>
    /// Calculates distance from the departure to the destination.
    /// </summary>
    /// <param name="departure">The point of departure.</param>
    /// <param name="distance">The distance function</param>
    /// <param name="usecache">Use cached values</param>
    /// <returns>The distance from the departure to the destination.</returns>
    public float From(IndexNode departure, bool usecache = true)
    {
      float result;
      if (usecache && this._cache.TryGetValue(departure, out result))
        return result;

      result = options.DefaultDistanceFunction(departure, destination);
      this._cache.TryAdd(departure, result);
      return result;
    }

    /// <summary>
    /// Compares 2 points by the distance from the destination.
    /// </summary>
    /// <param name="x">Left point.</param>
    /// <param name="y">Right point.</param>
    /// <returns>
    /// -1 if x is closer to the destination than y;
    /// 0 if x and y are equally far from the destination;
    /// 1 if x is farther from the destination than y.
    /// </returns>
    public int Compare(IndexNode x, IndexNode y)
    {
      var fromX = this.From(x);
      var fromY = this.From(y);
      return DistanceComparer.Compare(fromX, fromY);
    }
  }
}