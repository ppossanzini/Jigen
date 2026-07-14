using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.Filtering;
using Jigen.Proto;

namespace Jigen.Grpc;

public class Server(IHikyaku mediator, IHikyaku hikyaku)
  : Jigen.Proto.StoreCollectionService.StoreCollectionServiceBase
{
  public override async Task<EmbeddingResponse> CalculateEmbeddings(EmbeddingRequest request, ServerCallContext context)
  {
    var result =  await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings()
    {
      Task = request.Task,
      Sentence = request.Message
    });
    var response = new EmbeddingResponse();
    response.Embeddings.AddRange(result);
    return response;
  }

  public override async Task<EmbeddingBatchResponse> CalculateEmbeddingsBatch(EmbeddingBatchRequest request, ServerCallContext context)
  {
    var response = new EmbeddingBatchResponse();
    for (var i = 0; i < request.Messages.Count; i++)
      response.Results.Add(new EmbeddingResponse());

    // Blank inputs would fail the whole batch in the generator: keep an empty
    // row for them and embed only the meaningful ones, preserving positions.
    var indexes = new List<int>(request.Messages.Count);
    for (var i = 0; i < request.Messages.Count; i++)
      if (!string.IsNullOrWhiteSpace(request.Messages[i]))
        indexes.Add(i);

    if (indexes.Count == 0)
      return response;

    var vectors = await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddingsBatch
    {
      Task = request.Task,
      Sentences = indexes.Select(i => request.Messages[i]).ToArray()
    }, context.CancellationToken);

    for (var i = 0; i < indexes.Count; i++)
      response.Results[indexes[i]].Embeddings.AddRange(vectors[i]);

    return response;
  }

  public override async Task<RawContentResult> GetContent(ItemKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetRawContent()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray()
    });
    // Missing key: the client contract is an empty Content, not an error.
    return new RawContentResult() { Content = result is null ? ByteString.Empty : ByteString.CopyFrom(result) };
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

  public override async Task<IngestResult> SetVectors(IAsyncStreamReader<Vector> requestStream, ServerCallContext context)
  {
    var accepted = 0;

    await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
    {
      await mediator.Send(new Core.Command.collections.SetVector()
      {
        Database = request.Database,
        Collection = request.Collection,
        Key = request.Key.ToByteArray(),
        Content = request.Content.ToByteArray(),
        Embeddings = request.Embeddings.ToArray()
      }, context.CancellationToken);

      accepted++;
    }

    return new IngestResult { Success = true, Accepted = accepted };
  }

  // Sentences are embedded in windows of this size: one embedding dispatch
  // (local queue hop, or remote round trip with a Kaido worker) per window
  // instead of one per document.
  private const int DocumentEmbeddingBatchSize = 64;

  public override async Task<IngestResult> SetDocuments(IAsyncStreamReader<Document> requestStream, ServerCallContext context)
  {
    var accepted = 0;
    var window = new List<Document>(DocumentEmbeddingBatchSize);

    await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
    {
      window.Add(request);
      if (window.Count >= DocumentEmbeddingBatchSize)
        accepted += await IngestDocumentsWindow(window, context.CancellationToken);
    }

    accepted += await IngestDocumentsWindow(window, context.CancellationToken);

    return new IngestResult { Success = true, Accepted = accepted };
  }

  private async Task<int> IngestDocumentsWindow(List<Document> window, CancellationToken cancellationToken)
  {
    if (window.Count == 0) return 0;

    var withSentence = window.Where(d => !string.IsNullOrWhiteSpace(d.Sentence)).ToArray();

    float[][] vectors = [];
    if (withSentence.Length > 0)
      vectors = await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddingsBatch
      {
        Sentences = withSentence.Select(d => d.Sentence).ToArray()
      }, cancellationToken);

    var embeddingsByDocument = new Dictionary<Document, float[]>(withSentence.Length);
    for (var i = 0; i < withSentence.Length; i++)
      embeddingsByDocument[withSentence[i]] = vectors[i];

    var ingested = 0;
    foreach (var document in window)
    {
      await mediator.Send(new Core.Command.collections.SetDocument()
      {
        Database = document.Database,
        Collection = document.Collection,
        Key = document.Key.ToByteArray(),
        Content = document.Content.ToByteArray(),
        Embeddings = embeddingsByDocument.TryGetValue(document, out var embedding) && embedding is { Length: > 0 }
          ? embedding
          : null
      }, cancellationToken);

      ingested++;
    }

    window.Clear();
    return ingested;
  }

  public override async Task<SearchVectorResponse> SearchVector(SearchVectorRequest request, ServerCallContext context)
  {
    var result = await mediator.Send(ApplyTuning(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = request.Embeddings.ToArray(),
      Top = request.Top,
      Filter = ToFilterExpression(request.Filter)
    }, request.Tuning), context.CancellationToken);

    return ToSearchResponse(result);
  }

  public override async Task<SearchVectorResponse> SearchDocument(SearchDocumentRequest request, ServerCallContext context)
  {
    if (string.IsNullOrWhiteSpace(request.Sentence))
      return new SearchVectorResponse();

    var embeddings =  await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings() { Sentence = request.Sentence });
    var filter = ToFilterExpression(request.Filter);
    var result = await mediator.Send(ApplyTuning(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = embeddings,
      Top = request.Top,
      Filter = filter
    }, request.Tuning), context.CancellationToken);

    return ToSearchResponse(result);
  }

  private static Core.Query.collections.SearchVector ApplyTuning(Core.Query.collections.SearchVector query, SearchTuning tuning)
  {
    if (tuning is null) return query;

    query.EfSearch = tuning.EfSearch;
    query.NoContent = tuning.NoContent;
    if (tuning.HasMinScore) query.MinScore = tuning.MinScore;
    return query;
  }

  private static SearchVectorResponse ToSearchResponse(IEnumerable<Core.Query.collections.SearchVectorResultItem> result)
  {
    return new SearchVectorResponse
    {
      Results =
      {
        result.Select(i => new SearchVectorResult
        {
          Key = ByteString.CopyFrom(i.Key),
          Content = i.Content is null ? ByteString.Empty : ByteString.CopyFrom(i.Content),
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

  private const int DefaultKeysChunkSize = 1000;

  public override async Task StreamKeys(StreamKeysRequest request, IServerStreamWriter<KeysResult> responseStream, ServerCallContext context)
  {
    var chunkSize = request.ChunkSize > 0 ? request.ChunkSize : DefaultKeysChunkSize;

    var keys = await mediator.Send(new Core.Query.collections.GetAllKeys()
    {
      Database = request.Database, Collection = request.Collection
    });

    var chunk = new KeysResult();
    foreach (var key in keys)
    {
      chunk.Keys.Add(ByteString.CopyFrom(key.Value));
      if (chunk.Keys.Count >= chunkSize)
      {
        await responseStream.WriteAsync(chunk, context.CancellationToken);
        chunk = new KeysResult();
      }
    }

    if (chunk.Keys.Count > 0)
      await responseStream.WriteAsync(chunk, context.CancellationToken);
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