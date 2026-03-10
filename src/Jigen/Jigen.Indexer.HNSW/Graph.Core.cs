// <copyright file="Graph.Core.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>


// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jigen.Indexer
{
  internal partial class Graph<TItem, TDistance>
  {
    internal class Core
    {
      private readonly Func<TItem, TItem, TDistance> Distance;

      private DistanceCache<TDistance> DistanceCache;

      private long DistanceCalculationsCount;

      internal List<Node> Nodes { get; private set; }

      internal List<TItem> Items { get; private set; }

      internal Jigen.Indexer.Algorithms.Algorithm<TItem, TDistance> Algorithm { get; private set; }

      internal SmallWorldParameters Parameters { get; private set; }

      internal float DistanceCacheHitRate => (float)(DistanceCache?.HitCount ?? 0) / DistanceCalculationsCount;

      internal Core(Func<TItem, TItem, TDistance> distance, SmallWorldParameters parameters)
      {
        Distance = distance;
        Parameters = parameters;

        var initialSize = Math.Max(1024, parameters.InitialItemsSize);

        Nodes = new List<Node>(initialSize);
        Items = new List<TItem>(initialSize);

        switch (Parameters.NeighbourHeuristic)
        {
          case NeighbourSelectionHeuristic.SelectSimple:
          {
            Algorithm = new Jigen.Indexer.Algorithms.Algorithm3<TItem, TDistance>(this);
            break;
          }
          case NeighbourSelectionHeuristic.SelectHeuristic:
          {
            Algorithm = new Jigen.Indexer.Algorithms.Algorithm4<TItem, TDistance>(this);
            break;
          }
        }

        if (Parameters.EnableDistanceCacheForConstruction)
        {
          DistanceCache = new DistanceCache<TDistance>();
          DistanceCache.Resize(parameters.InitialDistanceCacheSize, false);
        }

        DistanceCalculationsCount = 0;
      }

      internal IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProvideRandomValues generator)
      {
        int newCount = items.Count;

        var newIDs = new List<int>();
        Items.AddRange(items);
        DistanceCache?.Resize(newCount, false);

        int id0 = Nodes.Count;

        for (int id = 0; id < newCount; ++id)
        {
          Nodes.Add(Algorithm.NewNode(id0 + id, RandomLayer(generator, Parameters.LevelLambda)));
          newIDs.Add(id0 + id);
        }

        return newIDs;
      }

      internal void ResizeDistanceCache(int newSize)
      {
        if (newSize >= 0)
        {
          DistanceCache?.Resize(newSize, true);
        }
        else
        {
          DistanceCache = null;
        }
      }

      internal void Serialize(Stream stream)
      {
        MessagePackSerializer.Serialize(stream, Nodes);
      }

      internal bool NeedsOptimization()
      {
        if (Nodes.Count == 0) return false;

        int notCached = 0;
        foreach (var n in Nodes)
        {
          if (!n.IsCached)
          {
            notCached++;
          }
        }

        return notCached > 1000 && notCached > (0.1 * Nodes.Count);
      }

      internal void Optimize(INodeDataStore nodeDataStore)
      {
        var nodesSpan = CollectionsMarshal.AsSpan(Nodes);

        for (int i = 0; i < nodesSpan.Length; i++)
        {
          Node.FlattenToCache(ref nodesSpan[i], nodeDataStore);
        }
      }

      internal TItem[] Deserialize(IReadOnlyList<TItem> items, Stream stream, INodeDataStore nodeDataStore)
      {
        Nodes = MessagePackSerializer.Deserialize<List<Node>>(stream);

        var nodesSpan = CollectionsMarshal.AsSpan(Nodes);

        for (int i = 0; i < nodesSpan.Length; i++)
        {
          Node.FlattenToCache(ref nodesSpan[i], nodeDataStore);
        }

        var remainingItems = items.Skip(Nodes.Count).ToArray();
        Items.AddRange(items.Take(Nodes.Count));
        return remainingItems;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      internal TDistance GetDistance(int fromId, int toId)
      {
        DistanceCalculationsCount++;
        if (DistanceCache is object)
        {
          return DistanceCache.GetOrCacheValue(fromId, toId, GetDistanceSkipCache);
        }
        else
        {
          return Distance(Items[fromId], Items[toId]);
        }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private TDistance GetDistanceSkipCache(int fromId, int toId)
      {
        return Distance(Items[fromId], Items[toId]);
      }

      private static int RandomLayer(IProvideRandomValues generator, double lambda)
      {
        var r = -Math.Log(generator.NextFloat()) * lambda;
        return (int)r;
      }
    }
  }
}