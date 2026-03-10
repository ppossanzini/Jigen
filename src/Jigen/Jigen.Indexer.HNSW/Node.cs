// <copyright file="Node.cs" company="Microsoft">
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License.
// </copyright>

// <copyright>
//  Changes Copyright Paolo Possanzini
//  Licensed under Apache 2.0
// </copyright>


using System.Runtime.CompilerServices;

namespace Jigen.Indexer
{
  /// <summary>
  /// The implementation of the node in hnsw graph.
  /// </summary>
  public struct Node(long position, Memory<long> connections, ReadOnlyMemory<byte> id, int maxLayers)
  {
    // Position of node in file
    public long Position = position;
    public int MaxLayers = maxLayers;

    // Id is in byte[] to allow direct connections with store ids.
    public ReadOnlyMemory<byte> Id = id;

    // Connections is a flattened list of long because it represents node connections in the graph.
    // is not an index but the file current position of connected nodes. 
    // Connections are bidirectional, meaning that if node A is connected to node B, node B is also connected to node A.
    // This decreases the number of unidirectional connections, as each connection is stored twice, but 
    // allows node removal conveniently 
    public Memory<long> Connections = connections;

    /// <summary>
    /// Gets the max layer where the node is presented.
    /// </summary>
    public int MaxLayer => MaxLayers - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (int from, int length) GetLayerBoundaries(int layer, int M)
    {
      return (
        from: layer == 0 ? 0 : (layer + 1) * M,
        length: layer == 0 ? 2 * M : M
      );
    }

    /// <summary>
    /// Gets connections ids of the node at the given layer
    /// </summary>
    /// <param name="layer">The layer to get connections at.</param>
    /// <returns>The connections of the node at the given layer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<long> GetLayer(int layer, int M)
    {
      var boundaries = GetLayerBoundaries(layer, M);
      return Connections.Slice(boundaries.from, boundaries.length);
    }

    public ReadOnlyMemory<long> EnumerateLayer(int layer, int M)
    {
      var boundaries = GetLayerBoundaries(layer, M);
      return Connections.Slice(boundaries.from, boundaries.length);
    }

    public void SetLayer(int layer, List<long> layerContent, int M)
    {
      var boundaries = GetLayerBoundaries(layer, M);

      var destination = Connections.Slice(boundaries.from, boundaries.length);
      destination.Span.Clear();

      layerContent.ToArray().AsMemory().CopyTo(destination);
    }

    internal List<long> GetLayerForModifying(int layer, int M)
    {
      return GetLayer(layer, M).ToArray().ToList();
    }
  }
}