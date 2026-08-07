using Google.Protobuf.Collections;

namespace Jigen.Client;

public static class EmbeddingExtensions
{
 
 

  public static float[] CalculateEmbeddings(this Context store, string sentence, string task = null)
  {
    return store.ServiceClient.CalculateEmbeddings(new Proto.EmbeddingRequest()
    {
      Message = sentence,
      Task = task ?? ""
    }).Embeddings.ToArray();
  }

  public static async Task<float[]> CalculateEmbeddingsAsync(this Context store, string sentence, string task = null)
  {
    var response = await store.ServiceClient.CalculateEmbeddingsAsync(new Proto.EmbeddingRequest()
    {
      Message = sentence,
      Task = task ?? ""
    });
    return response.Embeddings.ToArray();
  }

  public static IEnumerable<float[]> CalculateEmbeddingsBatch(this Context store, IEnumerable<string> sentences, string task = null)
  {
    return store.ServiceClient.CalculateEmbeddingsBatch(new Proto.EmbeddingBatchRequest()
    {
      Messages = { sentences },
      Task = task ?? ""
    }).Results.Select(result => result.Embeddings.ToArray());
  }

  public static async Task<IEnumerable<float[]>> CalculateEmbeddingsBatchAsync(this Context store, IEnumerable<string> sentences, string task = null)
  {
    var response = await store.ServiceClient.CalculateEmbeddingsBatchAsync(new Proto.EmbeddingBatchRequest()
    {
      Messages = { sentences },
      Task = task ?? ""
    });
    return response.Results.Select(result => result.Embeddings.ToArray());
  }
}