using System.Collections.Concurrent;
using System.Numerics;
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

            fixed (float* pQuery = queryVector)
            {
              float similarity = DotProductSimdUnsafe(pQuery, vectorBase, i.dimensions);
              topResults.Add((id, similarity));
            }
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

  private static unsafe float DotProductSimdUnsafe(float* leftPtr, float* rightPtr, int length)
  {
    int simdWidth = Vector<float>.Count;
    Vector<float> sumVector = Vector<float>.Zero;
    int i = 0;

    for (; i <= length - simdWidth; i += simdWidth)
    {
      // Caricamento diretto da puntatore a registro SIMD
      var v1 = *(Vector<float>*)(leftPtr + i);
      var v2 = *(Vector<float>*)(rightPtr + i);
      sumVector += v1 * v2;
    }

    // Somma orizzontale dei componenti del vettore
    float result = Vector.Sum(sumVector);

    // Gestione dei rimanenti elementi (tail cleanup)
    for (; i < length; i++) result += leftPtr[i] * rightPtr[i];

    return result;
  }
}