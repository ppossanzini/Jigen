using System.Numerics.Tensors;
using System.Text.Json;
using Jigen.DataStructures;
using Jigen.Filtering;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Indexers;

public class BruteForceIndexer : IIndexer
{
  public void AddToIndex(VectorEntry entry, bool waitForIndexing = false)
  {
  }

  public void RemoveFromIndex(string collection, byte[] key)
  {
  }

  public IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top, IFilterExpression contentFilter = null)
  {
    if (top <= 0) yield break;
    if (!store.GetCollectionIndexOf(collection, out var index)) yield break;

    // With a content filter the top-k cannot be decided from scores alone
    // (filtered-out entries must be backfilled): rank everything and stream.
    // Without a filter, a bounded per-thread selection avoids materializing
    // N ids and the O(N log N) global sort.
    var candidates = contentFilter is null
      ? SelectTopK(store, index, queryVector, top)
      : RankAll(store, index, queryVector);

    var count = 0;
    foreach (var candidate in candidates)
    {
      var content = store.GetContent(collection, candidate.Id);
      if (content is null)
        continue;

      if (contentFilter != null && !MatchesFilter(content, contentFilter))
        continue;

      yield return (new VectorEntry
      {
        Id = candidate.Id,
        CollectionName = collection,
        Content = content
      }, candidate.Score);

      count++;
      if (count >= top)
        break;
    }
  }

  /// <summary>
  /// Exact top-k selection: each worker keeps a bounded min-heap of
  /// (offset, score) — ids are materialized only for the k winners.
  /// </summary>
  private static List<(byte[] Id, float Score)> SelectTopK(
    IStore store,
    IReadOnlyDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)> index,
    float[] queryVector,
    int top)
  {
    var merged = new TopK(top);
    var mergeLock = new object();

    using var accessor = store.GetEmbeddingAccessor(0, 0);
    // Pointers cannot be captured by lambdas: carry the base address as nint.
    var basePointer = AcquirePointer(accessor);
    try
    {
      Parallel.ForEach(
        index.Values,
        () => new TopK(top),
        (i, _, local) =>
        {
          // Content-only entries have no embedding record: position 0 is
          // the file header, not a vector.
          if (i.embeddingsposition <= 0 || i.dimensions <= 0) return local;

          var score = ScoreAt(basePointer, i.embeddingsposition, i.dimensions, queryVector);
          if (!float.IsNaN(score))
            local.Add(i.embeddingsposition, score);
          return local;
        },
        local =>
        {
          lock (mergeLock)
            merged.MergeFrom(local);
        });

      // Ids only for the winners: read back from the record headers.
      var winners = merged.ItemsDescending();
      var results = new List<(byte[] Id, float Score)>(winners.Count);
      foreach (var (offset, score) in winners)
        results.Add((ReadIdAt(basePointer, offset), score));

      return results;
    }
    finally
    {
      accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }
  }

  /// <summary>Full ranking (filtered searches): every candidate, ordered by score.</summary>
  private static IEnumerable<(byte[] Id, float Score)> RankAll(
    IStore store,
    IReadOnlyDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)> index,
    float[] queryVector)
  {
    var results = new List<(byte[] Id, float Score)>(index.Count);
    var collectLock = new object();

    using var accessor = store.GetEmbeddingAccessor(0, 0);
    var basePointer = AcquirePointer(accessor);
    try
    {
      Parallel.ForEach(
        index.Values,
        () => new List<(byte[] Id, float Score)>(),
        (i, _, local) =>
        {
          if (i.embeddingsposition <= 0 || i.dimensions <= 0) return local;

          var score = ScoreAt(basePointer, i.embeddingsposition, i.dimensions, queryVector);
          if (!float.IsNaN(score))
            local.Add((ReadIdAt(basePointer, i.embeddingsposition), score));
          return local;
        },
        local =>
        {
          lock (collectLock)
            results.AddRange(local);
        });
    }
    finally
    {
      accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }

    results.Sort(static (a, b) => b.Score.CompareTo(a.Score));
    return results;
  }

  private static unsafe nint AcquirePointer(System.IO.MemoryMappedFiles.MemoryMappedViewAccessor accessor)
  {
    byte* pointer = null;
    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
    return (nint)pointer;
  }

  // Cosine similarity, the same metric the HNSW indexer ranks by: raw dot
  // products would disagree with it whenever stored embeddings are not
  // already unit-normalized.
  private static unsafe float ScoreAt(nint basePointer, long offset, int dimensions, float[] queryVector)
  {
    var record = (byte*)basePointer + offset;
    var idSize = *(int*)record;
    var vector = new ReadOnlySpan<float>(record + sizeof(int) + idSize, dimensions);
    return TensorPrimitives.CosineSimilarity(queryVector, vector);
  }

  private static unsafe byte[] ReadIdAt(nint basePointer, long offset)
  {
    var record = (byte*)basePointer + offset;
    var idSize = *(int*)record;
    return new ReadOnlySpan<byte>(record + sizeof(int), idSize).ToArray();
  }

  /// <summary>Bounded min-heap by score: keeps the k highest-scoring items.</summary>
  private sealed class TopK(int k)
  {
    private readonly (long Offset, float Score)[] _items = new (long, float)[k];
    private int _count;

    public void Add(long offset, float score)
    {
      if (_count < _items.Length)
      {
        _items[_count] = (offset, score);
        SiftUp(_count);
        _count++;
        return;
      }

      if (score <= _items[0].Score) return;

      _items[0] = (offset, score);
      SiftDown();
    }

    public void MergeFrom(TopK other)
    {
      for (var i = 0; i < other._count; i++)
        Add(other._items[i].Offset, other._items[i].Score);
    }

    public List<(long Offset, float Score)> ItemsDescending()
    {
      var list = new List<(long, float)>(_count);
      for (var i = 0; i < _count; i++)
        list.Add(_items[i]);
      list.Sort(static (a, b) => b.Item2.CompareTo(a.Item2));
      return list;
    }

    private void SiftUp(int i)
    {
      while (i > 0)
      {
        var parent = (i - 1) >> 1;
        if (_items[parent].Score <= _items[i].Score) break;
        (_items[parent], _items[i]) = (_items[i], _items[parent]);
        i = parent;
      }
    }

    private void SiftDown()
    {
      var i = 0;
      while (true)
      {
        var left = 2 * i + 1;
        if (left >= _count) break;
        var right = left + 1;
        var smallest = right < _count && _items[right].Score < _items[left].Score ? right : left;
        if (_items[i].Score <= _items[smallest].Score) break;
        (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
        i = smallest;
      }
    }
  }

  public IEnumerable<VectorEntry> Search(IStore store, string collection, IFilterExpression contentFilter = null)
  {
    if (!store.GetCollectionIndexOf(collection, out var index))
      yield break;
    foreach (var key in index.Keys)
    {
      var content = store.GetContent(collection, key);
      if (content is null)
        continue;

      if (contentFilter != null && !MatchesFilter(content, contentFilter))
        continue;

      yield return new VectorEntry
      {
        Id = key,
        CollectionName = collection,
        Content = content
      };
    }
  }

  private static bool MatchesFilter(ReadOnlyMemory<byte> serializedContent, IFilterExpression filter)
  {
    if (filter == null) return true;

    try
    {
      var json = MessagePackSerializer.ConvertToJson(serializedContent, MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance));
      using var doc = JsonDocument.Parse(json);
      return filter.Matches(doc.RootElement);
    }
    catch
    {
      return false;
    }
  }
}
