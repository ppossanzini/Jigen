// <copyright file="SmallWorld.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

namespace Jigen.Indexer
{
  /// <summary>
  /// <see href="https://arxiv.org/abs/1603.09320">Hierarchical Navigable Small World Graphs</see>.
  /// This class is a wrapper around the <see cref="Graph"/> class to provide a simpler interface and allow simpler use of generics
  /// </summary>
  /// <typeparam name="TItem">The type of items to connect into small world.</typeparam>
  /// <typeparam name="TDistance">The type of distance between items (expect any numeric type: float, double, decimal, int, ...).</typeparam>
  public partial class SmallWorld<TItem, TDistance>
    where TDistance : IComparable<TDistance>
  {
    /// <summary>
    /// Distance function in the items space.
    /// </summary>
    private readonly Func<TItem, TItem, TDistance> _distanceFunction;

    /// <summary>
    /// Hierarchical small world graph instance.
    /// </summary>
    private Graph _graph;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmallWorld{TItem, TDistance}"/> class.
    /// </summary>
    /// <param name="distanceFunction">The distance funtion to use in the small world.</param>
    public SmallWorld(Func<TItem, TItem, TDistance> distanceFunction)
    {
      this._distanceFunction = distanceFunction;
    }


    /// <summary>
    /// Builds hnsw graph from the items.
    /// </summary>
    /// <param name="items">The items to connect into the graph.</param>
    /// <param name="generator">The random number generator for building graph.</param>
    /// <param name="smallWorldParameters">Parameters of the algorithm.</param>
    public void BuildGraph(IList<TItem> items, Random generator, SmallWorldParameters smallWorldParameters)
    {
      var graph = new Graph(this._distanceFunction, smallWorldParameters);
      graph.Create(items, generator);
      this._graph = graph;
    }

    /// <summary>
    /// Run knn search for a given item.
    /// </summary>
    /// <param name="item">The item to search nearest neighbours.</param>
    /// <param name="k">The number of nearest neighbours.</param>
    /// <returns>The list of found nearest neighbours.</returns>
    public IList<KNNSearchResult> KNNSearch(TItem item, int k)
    {
      var destination = this._graph.CreateNewNode(-1, item, 0);
      var neighbourhood = this._graph.KNearest(destination, k);
      return neighbourhood.Select(n => new KNNSearchResult
      {
        Id = n.Id,
        Item = n.Item,
        Distance = destination.TravelingCosts.From(n),
      }).ToList();
    }
  }
}