// <copyright file="Node.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

namespace Jigen.Indexer
{
  internal partial class Algorithms
  {
    /// <summary>
    /// The abstract class representing algorithm to control node capacity.
    /// </summary>
    /// <typeparam name="TItem">The typeof the items in the small world.</typeparam>
    /// <typeparam name="TDistance">The type of the distance in the small world.</typeparam>
    internal abstract class Algorithm<TItem, TDistance>(Graph<TItem, TDistance>.Core graphCore)
      where TDistance : struct, IComparable<TDistance>
    {
      protected readonly Graph<TItem, TDistance>.Core GraphCore = graphCore;

      protected readonly Func<int, int, TDistance> NodeDistance = graphCore.GetDistance;

      /// <summary>
      /// Creates a new instance of the <see cref="Node"/> struct. Controls the exact type of connection lists.
      /// </summary>
      /// <param name="nodeId">The identifier of the node.</param>
      /// <param name="maxLayer">The max layer where the node is presented.</param>
      /// <returns>The new instance.</returns>
      internal virtual Node NewNode(ReadOnlyMemory<byte> nodeId, int maxLayer)
      {
        return new Node(nodeId, maxLayer, this.GraphCore.Parameters.M);
      }

      /// <summary>
      /// The algorithm which selects best neighbours from the candidates for the given node.
      /// </summary>
      /// <param name="candidatesIds">The identifiers of candidates to neighbourhood.</param>
      /// <param name="travelingCosts">Traveling costs to compare candidates.</param>
      /// <param name="layer">The layer of the neighbourhood.</param>
      /// <returns>Best nodes selected from the candidates.</returns>
      internal abstract List<int> SelectBestForConnecting(List<int> candidatesIds, TravelingCosts<int, TDistance> travelingCosts, int layer);

      /// <summary>
      /// Get maximum allowed connections for the given level.
      /// </summary>
      /// <remarks>
      /// Article: Section 4.1:
      /// "Selection of the Mmax0 (the maximum number of connections that an element can have in the zero layer) also
      /// has a strong influence on the search performance, especially in case of high quality(high recall) search.
      /// Simulations show that setting Mmax0 to M(this corresponds to kNN graphs on each layer if the neighbors
      /// selection heuristic is not used) leads to a very strong performance penalty at high recall.
      /// Simulations also suggest that 2∙M is a good choice for Mmax0;
      /// setting the parameter higher leads to performance degradation and excessive memory usage."
      /// </remarks>
      /// <param name="layer">The level of the layer.</param>
      /// <returns>The maximum number of connections.</returns>
      internal int GetM(int layer)
      {
        return layer == 0 ? 2 * GraphCore.Parameters.M : GraphCore.Parameters.M;
      }

      /// <summary>
      /// Tries to connect the node with the new neighbour.
      /// </summary>
      /// <param name="node">The node to add neighbour to.</param>
      /// <param name="neighbour">The new neighbour.</param>
      /// <param name="layer">The layer to add neighbour to.</param>
      internal void Connect(ref Node node, ref Node neighbour, int layer)
      {
        var nodeLayer = node.GetLayerForModifying(layer);
        nodeLayer.Add(neighbour.Id);
        if (nodeLayer.Count > GetM(layer))
        {
          var travelingCosts = new TravelingCosts<int, TDistance>(NodeDistance, node.Id);
          node.SetLayer(layer, SelectBestForConnecting(nodeLayer, travelingCosts, layer));
        }
      }
    }
  }
}