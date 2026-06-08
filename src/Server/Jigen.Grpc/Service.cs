using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.Filtering;
using Jigen.Proto;
using Jigen.SemanticTools;

namespace Jigen.Grpc;

public class Server(IHikyaku mediator, IEmbeddingGenerator embeddingGenerator)
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

  public override async Task<SearchVectorResponse> SearchVector(SearchVectorRequest request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = request.Embeddings.ToArray(),
      Top = request.Top
    });

    return new SearchVectorResponse
    {
      Results =
      {
        result.Select(i => new SearchVectorResult
        {
          Key = ByteString.CopyFrom(i.Key),
          Content = ByteString.CopyFrom(i.Content),
          Score = i.Score
        })
      }
    };
  }

  public override async Task<SearchVectorResponse> SearchDocument(SearchDocumentRequest request, ServerCallContext context)
  {
    if (string.IsNullOrWhiteSpace(request.Sentence))
      return new SearchVectorResponse();

    var embeddings = embeddingGenerator.GenerateEmbedding(request.Sentence);
    var filter = ToFilterExpression(request.Filter);
    var result = await mediator.Send(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = embeddings,
      Top = request.Top,
      Filter = filter
    });

    return new SearchVectorResponse
    {
      Results =
      {
        result.Select(i => new SearchVectorResult
        {
          Key = ByteString.CopyFrom(i.Key),
          Content = ByteString.CopyFrom(i.Content),
          Score = i.Score
        })
      }
    };
  }

  private static IFilterExpression ToFilterExpression(FilterNode node)
  {
    if (node == null || node.KindCase == FilterNode.KindOneofCase.None)
      return null;

    return node.KindCase switch
    {
      FilterNode.KindOneofCase.Equals_ => new PropertyEqualsFilter
      {
        PropertyPath = node.Equals_.PropertyPath,
        Value = ToValue(node.Equals_.Value)
      },
      FilterNode.KindOneofCase.CollectionAny => new PropertyCollectionAnyFilter
      {
        PropertyPath = node.CollectionAny.PropertyPath,
        Value = ToValue(node.CollectionAny.Value)
      },
      FilterNode.KindOneofCase.And => new AndFilter
      {
        Left = ToFilterExpression(node.And.Left),
        Right = ToFilterExpression(node.And.Right)
      },
      FilterNode.KindOneofCase.Or => new OrFilter
      {
        Left = ToFilterExpression(node.Or.Left),
        Right = ToFilterExpression(node.Or.Right)
      },
      _ => null
    };
  }

  private static object ToValue(FilterValue value)
  {
    if (value == null || value.KindCase == FilterValue.KindOneofCase.None)
      return null;

    return value.KindCase switch
    {
      FilterValue.KindOneofCase.StringValue => value.StringValue,
      FilterValue.KindOneofCase.IntValue => value.IntValue,
      FilterValue.KindOneofCase.LongValue => value.LongValue,
      FilterValue.KindOneofCase.DoubleValue => value.DoubleValue,
      FilterValue.KindOneofCase.BoolValue => value.BoolValue,
      FilterValue.KindOneofCase.NullValue => null,
      _ => null
    };
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