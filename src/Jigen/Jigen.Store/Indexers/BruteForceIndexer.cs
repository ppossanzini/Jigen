using System.Collections.Concurrent;
using System.Numerics;
using System.Numerics.Tensors;
using Jigen.DataStructures;

namespace Jigen.Indexers;

public class BruteForceIndexer : IIndexer
{
  public void CreateEmptyIndex(string collection)
  {
  }

  public void AddToIndex(VectorEntry entry)
  {
  }

  public void UpdateIndex(VectorEntry entry)
  {
  }

  public void RemoveFromIndex(VectorEntry entry)
  {
  }

  public unsafe List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top)
  {
    var topResults = new ConcurrentBag<(byte[] Id, float Score)>();
    if (!store.GetCollectionIndexOf(collection, out var index)) return [];

    using (var accessor = store.GetEmbeddingAccessor(0, 0))
    {
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

    return topResults.OrderByDescending(r => r.Score).Take(top)
      .Select(r => (new VectorEntry
      {
        Id = r.Id,
        Content = store.GetContent(collection, r.Id)
      }, r.Score))
      .ToList();
  }
}