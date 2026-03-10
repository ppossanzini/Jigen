// <copyright>
// Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>


namespace Jigen.Indexer
{
  /// <summary>
  /// Abstraction for the flattened node connection storage.
  /// Allows swapping in-memory (CachedNodeData) with disk-backed implementations.
  /// </summary>
  public interface INodeDataStore
  {
    ReadOnlySpan<int> GetLayer(int bucketIndex, int position, int layerIndex, int maxLayer);
    ReadOnlySpan<int> GetAll(int bucketIndex, int position, int maxLayers);

    (int bucketIndex, int position, int maxLayers) Add(List<List<int>> connections);
    (int bucketIndex, int position, int maxLayers) Add(ReadOnlySpan<int> data, int maxLayer);
  }
}