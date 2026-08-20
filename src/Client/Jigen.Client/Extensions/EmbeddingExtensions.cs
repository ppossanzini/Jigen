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

  public static float[] CalculateImageEmbedding(this Context store, byte[] image)
  {
    return store.ServiceClient.CalculateImageEmbedding(new Proto.ImageEmbeddingRequest()
    {
      Image = Google.Protobuf.ByteString.CopyFrom(image)
    }).Embeddings.ToArray();
  }

  public static async Task<float[]> CalculateImageEmbeddingAsync(this Context store, byte[] image)
  {
    var response = await store.ServiceClient.CalculateImageEmbeddingAsync(new Proto.ImageEmbeddingRequest()
    {
      Image = Google.Protobuf.ByteString.CopyFrom(image)
    });
    return response.Embeddings.ToArray();
  }

  public static IEnumerable<float[]> CalculateImageEmbeddingsBatch(this Context store, IEnumerable<byte[]> images)
  {
    return store.ServiceClient.CalculateImageEmbeddingBatch(new Proto.ImageEmbeddingBatchRequest()
    {
      Images = { images.Select(Google.Protobuf.ByteString.CopyFrom) }
    }).Results.Select(result => result.Embeddings.ToArray());
  }

  public static async Task<IEnumerable<float[]>> CalculateImageEmbeddingsBatchAsync(this Context store, IEnumerable<byte[]> images)
  {
    var response = await store.ServiceClient.CalculateImageEmbeddingBatchAsync(new Proto.ImageEmbeddingBatchRequest()
    {
      Images = { images.Select(Google.Protobuf.ByteString.CopyFrom) }
    });
    return response.Results.Select(result => result.Embeddings.ToArray());
  }

  public static IEnumerable<float[]> CalculateImageTileEmbeddings(this Context store, byte[] image)
  {
    return store.ServiceClient.CalculateImageTileEmbeddings(new Proto.ImageEmbeddingRequest()
    {
      Image = Google.Protobuf.ByteString.CopyFrom(image)
    }).Tiles.Select(tile => tile.Embeddings.ToArray());
  }

  public static async Task<IEnumerable<float[]>> CalculateImageTileEmbeddingsAsync(this Context store, byte[] image)
  {
    var response = await store.ServiceClient.CalculateImageTileEmbeddingsAsync(new Proto.ImageEmbeddingRequest()
    {
      Image = Google.Protobuf.ByteString.CopyFrom(image)
    });
    return response.Tiles.Select(tile => tile.Embeddings.ToArray());
  }
}