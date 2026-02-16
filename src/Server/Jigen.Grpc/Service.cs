using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.Proto;
using Jigen.SemanticTools;

namespace Jigen.Grpc;

public class Server(ILogger<Server> logger, IHikyaku mediator, IEmbeddingGenerator embeddingGenerator)
  : Jigen.Proto.StoreCollectionService.StoreCollectionServiceBase
{
  public override Task<EmbeddingResponse> CalculateEmbeddings(EmbeddingRequest request, ServerCallContext context)
  {
    var result = embeddingGenerator.GenerateEmbedding(request.Message);
    var response = new EmbeddingResponse();
    response.Embeddings.AddRange(result);
    return Task.FromResult(response);
  }

  public override async Task<RawContentResult> GetContent(ItemKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetRawContent()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray()
    });
    return new RawContentResult() { Content = ByteString.CopyFrom(result) };
  }

  public override async Task<ListCollectionResult> ListCollections(CollectionKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.ListCollections() { Database = request.Database });
    return new ListCollectionResult() { Collections = { result } };
  }

  public override async Task<Result> SetDocument(Document request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.SetDocument()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Sentence = request.Sentence
    });

    return new Result() { Success = true };
  }

  public override async Task<Result> SetVector(Vector request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.SetVector()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Embeddings = request.Embeddings.ToArray()
    });

    return new Result() { Success = true };
  }

  public override async Task<Result> Clear(CollectionKey request, ServerCallContext context)
  {
    await mediator.Send(new Jigen.Core.Command.collections.Clear()
    {
      Database = request.Database, Collection = request.Collection
    });
    return new Result() { Success = true };
  }

  public override async Task<CountResult> Count(CollectionKey request, ServerCallContext context)
  {
    return new CountResult()
    {
      Count = await mediator.Send(new Jigen.Core.Command.collections.Count()
      {
        Database = request.Database, Collection = request.Collection
      })
    };
  }

  public override async Task<Result> Contains(ItemKey request, ServerCallContext context)
  {
    return
      new Result()
      {
        Success = await mediator.Send(new Core.Command.collections.Contains()
        {
          Database = request.Database, Collection = request.Collection, Key = request.Key.ToByteArray()
        })
      };
  }

  public override async Task<KeysResult> GetAllKeys(CollectionKey request, ServerCallContext context)
  {
    var result = (await mediator.Send(new Core.Query.collections.GetAllKeys()
    {
      Database = request.Database, Collection = request.Collection
    })).Select(i => ByteString.CopyFrom(i.Value));

    return new KeysResult()
    {
      Keys = { result }
    };
  }

  public override async Task<Result> DeleteVector(ItemKey request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.DeleteVector()
    {
      Database = request.Database, Collection = request.Collection, Key = request.Key.ToByteArray()
    });
    return new Result() { Success = true };
  }
  
}