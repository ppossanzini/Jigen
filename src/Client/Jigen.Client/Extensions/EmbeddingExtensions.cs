using Google.Protobuf.Collections;

namespace Jigen.Client;

public static class EmbeddingExtensions
{
 
 

  public static float[] CalculateEmbeddings(this Context store, string sentence, string task = null, string model = null)
  {
    return store.ServiceClient.CalculateEmbeddings(new Proto.EmbeddingRequest()
    {
      Message = sentence,
      Task = task ?? "",
      Model = model ?? ""
    }).Embeddings.ToArray();
  }

  public static async Task<float[]> CalculateEmbeddingsAsync(this Context store, string sentence, string task = null, string model = null)
  {
    var response = await store.ServiceClient.CalculateEmbeddingsAsync(new Proto.EmbeddingRequest()
    {
      Message = sentence,
      Task = task ?? "",
      Model = model ?? ""
    });
    return response.Embeddings.ToArray();
  }

  public static IEnumerable<float[]> CalculateEmbeddingsBatch(this Context store, IEnumerable<string> sentences, string task = null, string model = null)
  {
    return store.ServiceClient.CalculateEmbeddingsBatch(new Proto.EmbeddingBatchRequest()
    {
      Messages = { sentences },
      Task = task ?? "",
      Model = model ?? ""
    }).Results.Select(result => result.Embeddings.ToArray());
  }

  public static async Task<IEnumerable<float[]>> CalculateEmbeddingsBatchAsync(this Context store, IEnumerable<string> sentences, string task = null, string model = null)
  {
    var response = await store.ServiceClient.CalculateEmbeddingsBatchAsync(new Proto.EmbeddingBatchRequest()
    {
      Messages = { sentences },
      Task = task ?? "",
      Model = model ?? ""
    });
    return response.Results.Select(result => result.Embeddings.ToArray());
  }

  public static IReadOnlyList<Proto.PassageEmbeddingResult> CalculatePassageEmbeddings(
    this Context store,
    string sentence,
    int? passageTokenSize = null,
    int? passageOverlapSize = null,
    string task = null,
    string model = null)
  {
    var request = new Proto.PassageEmbeddingRequest
    {
      Message = sentence,
      Task = task ?? "",
      Model = model ?? ""
    };
    if (passageTokenSize.HasValue)
      request.PassageTokenSize = passageTokenSize.Value;
    if (passageOverlapSize.HasValue)
      request.PassageOverlapSize = passageOverlapSize.Value;
    return store.ServiceClient.CalculatePassageEmbeddings(request).Results;
  }

  public static async Task<IReadOnlyList<Proto.PassageEmbeddingResult>> CalculatePassageEmbeddingsAsync(
    this Context store,
    string sentence,
    int? passageTokenSize = null,
    int? passageOverlapSize = null,
    string task = null,
    string model = null,
    CancellationToken cancellationToken = default)
  {
    var request = new Proto.PassageEmbeddingRequest
    {
      Message = sentence,
      Task = task ?? "",
      Model = model ?? ""
    };
    if (passageTokenSize.HasValue)
      request.PassageTokenSize = passageTokenSize.Value;
    if (passageOverlapSize.HasValue)
      request.PassageOverlapSize = passageOverlapSize.Value;
    var response = await store.ServiceClient.CalculatePassageEmbeddingsAsync(request, cancellationToken: cancellationToken);
    return response.Results;
  }

  public static int CalculatePassageSplitCount(
    this Context store,
    string sentence,
    int? passageTokenSize = null,
    int? passageOverlapSize = null,
    string model = null)
  {
    var request = CreatePassageSplitCountRequest(sentence, passageTokenSize, passageOverlapSize, model);
    return store.ServiceClient.CalculatePassageSplitCount(request).Count;
  }

  public static async Task<int> CalculatePassageSplitCountAsync(
    this Context store,
    string sentence,
    int? passageTokenSize = null,
    int? passageOverlapSize = null,
    string model = null,
    CancellationToken cancellationToken = default)
  {
    var request = CreatePassageSplitCountRequest(sentence, passageTokenSize, passageOverlapSize, model);
    var response = await store.ServiceClient.CalculatePassageSplitCountAsync(request, cancellationToken: cancellationToken);
    return response.Count;
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

  private static Proto.PassageSplitCountRequest CreatePassageSplitCountRequest(
    string sentence,
    int? passageTokenSize,
    int? passageOverlapSize,
    string model)
  {
    var request = new Proto.PassageSplitCountRequest
    {
      Message = sentence,
      Model = model ?? ""
    };
    if (passageTokenSize.HasValue)
      request.PassageTokenSize = passageTokenSize.Value;
    if (passageOverlapSize.HasValue)
      request.PassageOverlapSize = passageOverlapSize.Value;
    return request;
  }
}
