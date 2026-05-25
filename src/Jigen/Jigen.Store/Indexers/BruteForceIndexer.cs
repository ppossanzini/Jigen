using System.Collections.Concurrent;
using System.Numerics;
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
    var topResults = new ConcurrentBag<(byte[] Id, float Score)>();
    if (!store.GetCollectionIndexOf(collection, out var index)) yield break;

    unsafe
    {
      using var accessor = store.GetEmbeddingAccessor(0, 0);
      try
      {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

        Parallel.ForEach(index.Values, i =>
        {
          try
          {
            var offset = i.embeddingsposition;
            var currentPtr = pointer + offset;

            int idsize = *(int*)currentPtr;

            var id = new byte[idsize];
            fixed (byte* idDst = id)
            {
              Buffer.MemoryCopy(
                source: currentPtr + sizeof(int),
                destination: idDst,
                destinationSizeInBytes: idsize,
                sourceBytesToCopy: idsize);
            }

            float* vectorBase = (float*)(currentPtr + sizeof(int) + idsize);

            ReadOnlySpan<float> query = queryVector;
            ReadOnlySpan<float> candidate = new ReadOnlySpan<float>(vectorBase, i.dimensions);

            float similarity = TensorPrimitives.Dot(query, candidate);
            topResults.Add((id, similarity));
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error processing vector entry : {ex.Message}");
            Console.WriteLine($"Error processing vector entry : {ex.StackTrace}");
            throw;
          }
        });
      }
      finally
      {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
      }
    }

    var orderedByScore = topResults.OrderByDescending(r => r.Score);
    var count = 0;

    foreach (var candidate in orderedByScore)
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

      if (count >= top)
        break;
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