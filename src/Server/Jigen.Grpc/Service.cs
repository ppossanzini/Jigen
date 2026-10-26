using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.DataStructures;
using Jigen.Filtering;
using Jigen.Proto;
using Jigen.Embedding.Core.Options;
using Microsoft.Extensions.Options;
using Jigen.Handlers;

namespace Jigen.Grpc;

public class Server(IHikyaku mediator, IHikyaku hikyaku, IOptions<PassageSplittingOptions> passageOptions, IOptions<JigenServerSettings> serverSettings)
  : Jigen.Proto.StoreCollectionService.StoreCollectionServiceBase
{
  public override async Task<EmbeddingResponse> CalculateEmbeddings(EmbeddingRequest request, ServerCallContext context)
  {
    var result =  await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateEmbeddings()
    {
      Model = request.Model,
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

    var vectors = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateEmbeddingsBatch
    {
      Model = request.Model,
      Task = request.Task,
      Sentences = indexes.Select(i => request.Messages[i]).ToArray()
    }, context.CancellationToken);

    for (var i = 0; i < indexes.Count; i++)
      response.Results[indexes[i]].Embeddings.AddRange(vectors[i]);

    return response;
  }

  public override async Task<SparseEmbeddingResponse> CalculateSparseEmbedding(EmbeddingRequest request,
    ServerCallContext context)
  {
    var sparse = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateSparseEmbedding
    {
      Model = request.Model,
      Sentence = request.Message
    }, context.CancellationToken);

    var response = new SparseEmbeddingResponse();
    response.Terms.AddRange(sparse.Terms);
    response.Weights.AddRange(sparse.Weights);
    return response;
  }

  public override async Task<SparseEmbeddingBatchResponse> CalculateSparseEmbeddingBatch(
    EmbeddingBatchRequest request, ServerCallContext context)
  {
    var response = new SparseEmbeddingBatchResponse();
    foreach (var message in request.Messages)
    {
      var sparse = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateSparseEmbedding
      {
        Model = request.Model,
        Sentence = message
      }, context.CancellationToken);

      var item = new SparseEmbeddingResponse();
      item.Terms.AddRange(sparse.Terms);
      item.Weights.AddRange(sparse.Weights);
      response.Results.Add(item);
    }

    return response;
  }

  public override async Task<PassageEmbeddingResponse> CalculatePassageEmbeddings(
    PassageEmbeddingRequest request,
    ServerCallContext context)
  {
    if (string.IsNullOrWhiteSpace(request.Message))
      throw new RpcException(new Status(StatusCode.InvalidArgument, "Message is required."));
    var size = request.HasPassageTokenSize ? request.PassageTokenSize : passageOptions.Value.DefaultTokenSize;
    var overlap = request.HasPassageOverlapSize ? request.PassageOverlapSize : passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      throw new RpcException(new Status(StatusCode.InvalidArgument,
        $"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}."));
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      throw new RpcException(new Status(StatusCode.InvalidArgument,
        $"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}."));

    var passages = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculatePassageEmbeddings
    {
      Model = request.Model,
      Task = request.Task,
      Sentence = request.Message,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, context.CancellationToken);

    var response = new PassageEmbeddingResponse();
    response.Results.AddRange(passages.Select(passage =>
    {
      var result = new PassageEmbeddingResult
      {
        Text = passage.Text,
        StartToken = passage.StartToken,
        TokenCount = passage.TokenCount
      };
      result.Embeddings.AddRange(passage.Embedding);
      return result;
    }));
    return response;
  }

  public override async Task<PassageSplitCountResponse> CalculatePassageSplitCount(
    PassageSplitCountRequest request,
    ServerCallContext context)
  {
    if (string.IsNullOrWhiteSpace(request.Message))
      throw new RpcException(new Status(StatusCode.InvalidArgument, "Message is required."));
    var size = request.HasPassageTokenSize ? request.PassageTokenSize : passageOptions.Value.DefaultTokenSize;
    var overlap = request.HasPassageOverlapSize ? request.PassageOverlapSize : passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      throw new RpcException(new Status(StatusCode.InvalidArgument,
        $"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}."));
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      throw new RpcException(new Status(StatusCode.InvalidArgument,
        $"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}."));

    var count = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculatePassageSplitCount
    {
      Model = request.Model,
      Sentence = request.Message,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, context.CancellationToken);

    return new PassageSplitCountResponse { Count = count };
  }

  public override async Task<EmbeddingResponse> CalculateImageEmbedding(ImageEmbeddingRequest request, ServerCallContext context)
  {
    if (request.Image.IsEmpty)
      return new EmbeddingResponse();

    var result = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateImageEmbedding()
    {
      ImageBytes = request.Image.ToByteArray()
    }, context.CancellationToken);

    var response = new EmbeddingResponse();
    response.Embeddings.AddRange(result);
    return response;
  }

  public override async Task<EmbeddingBatchResponse> CalculateImageEmbeddingBatch(ImageEmbeddingBatchRequest request, ServerCallContext context)
  {
    var response = new EmbeddingBatchResponse();
    for (var i = 0; i < request.Images.Count; i++)
      response.Results.Add(new EmbeddingResponse());

    // Empty inputs would fail the whole batch in the generator: keep an empty
    // row for them and embed only the meaningful ones, preserving positions.
    var indexes = new List<int>(request.Images.Count);
    for (var i = 0; i < request.Images.Count; i++)
      if (!request.Images[i].IsEmpty)
        indexes.Add(i);

    if (indexes.Count == 0)
      return response;

    var vectors = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateImageEmbeddingBatch
    {
      Images = indexes.Select(i => request.Images[i].ToByteArray()).ToArray()
    }, context.CancellationToken);

    for (var i = 0; i < indexes.Count; i++)
      response.Results[indexes[i]].Embeddings.AddRange(vectors[i]);

    return response;
  }

  public override async Task<ImageTileEmbeddingsResponse> CalculateImageTileEmbeddings(ImageEmbeddingRequest request, ServerCallContext context)
  {
    if (request.Image.IsEmpty)
      return new ImageTileEmbeddingsResponse();

    var result = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateImageTileEmbeddings()
    {
      ImageBytes = request.Image.ToByteArray()
    }, context.CancellationToken);

    var response = new ImageTileEmbeddingsResponse();
    foreach (var vector in result)
    {
      var tile = new EmbeddingResponse();
      tile.Embeddings.AddRange(vector);
      response.Tiles.Add(tile);
    }
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

  public override async Task<StoredEmbeddingsResponse> GetEmbeddings(ItemKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetEmbeddings()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray()
    });

    // Missing key or vector-less entry: empty Vectors, like GetContent.
    var response = new StoredEmbeddingsResponse();
    if (result is not null)
      foreach (var vector in result)
        response.Vectors.Add(new EmbeddingVector { Embeddings = { vector } });
    return response;
  }

  public override async Task<ListCollectionResult> ListCollections(CollectionKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.ListCollections() { Database = request.Database });
    return new ListCollectionResult() { Collections = { result } };
  }

  /// <summary>
  /// Sparse half of a query derived from its text: the same tokenizer pass that
  /// yields the dense embedding yields these terms, so a caller can search in
  /// hybrid with a sentence alone.
  /// </summary>
  private async Task<IReadOnlyDictionary<int, float>> SparseQueryFromSentence(string sentence,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return null;

    var sparse = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateSparseEmbedding
    {
      Sentence = sentence
    }, cancellationToken);

    if (sparse.Terms.Length == 0)
      return null;

    var query = new Dictionary<int, float>(sparse.Terms.Length);
    for (var i = 0; i < sparse.Terms.Length; i++)
      query[sparse.Terms[i]] = sparse.Weights[i];

    return query;
  }

  // Sparse terms arrive as term-id/weight maps. A negative term id cannot name
  // a term, so the whole request is rejected instead of indexing a document
  // that no query could ever match.
  private static SparseTerm[] ToSparseTerms(SparseTerms sparse)
  {
    if (sparse is null || sparse.Terms.Count == 0)
      return null;

    var terms = new SparseTerm[sparse.Terms.Count];
    var index = 0;
    foreach (var (term, weight) in sparse.Terms)
    {
      if (term < 0)
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Sparse term ids cannot be negative."));
      terms[index++] = new SparseTerm(term, weight);
    }

    return terms;
  }

  // One sparse list per vector, aligned by index with Vectors: a shorter list
  // leaves the trailing vectors without sparse terms, a longer one is refused
  // because it would attach terms to vectors that do not exist.
  private static SparseTerm[][] ToSparseVectors(Vector request)
  {
    if (request.Sparse.Count == 0)
      return null;

    if (request.Sparse.Count > request.Vectors.Count)
      throw new RpcException(new Status(StatusCode.InvalidArgument,
        "Sparse carries more term lists than Vectors; they are aligned by index."));

    return request.Sparse.Select(sparse => ToSparseTerms(sparse) ?? []).ToArray();
  }

  public override async Task<Result> SetDocument(Document request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.SetDocument()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Sentence = request.Sentence,
      SparseTerms = ToSparseTerms(request.Sparse),
      SparseFromSentence = request.SparseFromSentence
    });

    return new Result() { Success = true };
  }

  // The vector RPCs carry the entry's vectors and nothing else: a request with
  // an empty Vectors list is rejected instead of silently storing a document
  // without vectors (an old client sending the removed single-vector field
  // would otherwise look successful).
  private static float[][] RequireVectors(Vector request) =>
    request.Vectors.Count == 0
      ? throw new RpcException(new Status(StatusCode.InvalidArgument,
        "Vector requests carry every vector of the entry in Vectors; the request has none."))
      : request.Vectors.Select(v => v.Embeddings.ToArray()).ToArray();

  public override async Task<Result> SetVector(Vector request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.SetVector()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Vectors = RequireVectors(request),
      SparseVectors = ToSparseVectors(request)
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
        Vectors = RequireVectors(request),
        SparseVectors = ToSparseVectors(request)
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
      vectors = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateEmbeddingsBatch
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
        Vectors = embeddingsByDocument.TryGetValue(document, out var embedding) && embedding is { Length: > 0 }
          ? [embedding]
          : null,
        SparseTerms = ToSparseTerms(document.Sparse)
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
      Filter = ToFilterExpression(request.Filter),
      SparseQuery = request.SparseQuery.Count == 0 ? null : request.SparseQuery
    }, request.Tuning), context.CancellationToken);

    return ToSearchResponse(result);
  }

  public override async Task<SearchVectorResponse> SearchDocument(SearchDocumentRequest request, ServerCallContext context)
  {
    if (string.IsNullOrWhiteSpace(request.Sentence))
      return new SearchVectorResponse();

    var embeddings = await hikyaku.Send(new Jigen.Embedding.Core.Commands.CalculateEmbeddings
    {
      Sentence = request.Sentence,
      Task = serverSettings.Value.EmbeddingQueryTask
    }, context.CancellationToken);
    var filter = ToFilterExpression(request.Filter);

    // An explicit sparse query always wins; asking for the derived one fills the
    // half the caller did not send, from the same sentence that produced the
    // dense embedding.
    IReadOnlyDictionary<int, float> sparseQuery = request.SparseQuery.Count > 0
      ? request.SparseQuery
      : request.SparseFromSentence
        ? await SparseQueryFromSentence(request.Sentence, context.CancellationToken)
        : null;

    var result = await mediator.Send(ApplyTuning(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = embeddings,
      Top = request.Top,
      Filter = filter,
      SparseQuery = sparseQuery
    }, request.Tuning), context.CancellationToken);

    return ToSearchResponse(result);
  }

  private static Core.Query.collections.SearchVector ApplyTuning(Core.Query.collections.SearchVector query, SearchTuning tuning)
  {
    if (tuning is null) return query;

    query.EfSearch = tuning.EfSearch;
    query.NoContent = tuning.NoContent;
    if (tuning.HasMinDenseScore) query.MinDenseScore = tuning.MinDenseScore;
    if (tuning.HasMinSparseScore) query.MinSparseScore = tuning.MinSparseScore;
    return query;
  }

  private static SearchVectorResponse ToSearchResponse(IEnumerable<Core.Query.collections.SearchVectorResultItem> result)
  {
    return new SearchVectorResponse
    {
      Results =
      {
        result.Select(i =>
        {
          var item = new SearchVectorResult
          {
            Key = ByteString.CopyFrom(i.Key),
            Content = i.Content is null ? ByteString.Empty : ByteString.CopyFrom(i.Content),
            Score = i.Score
          };
          // Assigning the optional field is what marks it as present on the wire.
          if (i.DenseScore.HasValue) item.DenseScore = i.DenseScore.Value;
          if (i.SparseScore.HasValue) item.SparseScore = i.SparseScore.Value;
          return item;
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

  // ── New RPCs (added during Store API alignment) ──

  public override async Task<CollectionInfoResponse> GetCollectionInfo(CollectionKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionInfo()
    {
      Database = request.Database,
      Collection = request.Collection
    });

    return ToCollectionInfoResponse(result);
  }

  public override async Task<CollectionsInfoResponse> GetCollectionsInfo(CollectionKey request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionsInfo()
    {
      Database = request.Database
    });

    var response = new CollectionsInfoResponse();
    response.Collections.AddRange(result.Select(ToCollectionInfoResponse));
    return response;
  }

  public override async Task<GraphSnapshotResponse> GetCollectionGraph(GetGraphRequest request, ServerCallContext context)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionGraph()
    {
      Database = request.Database,
      Collection = request.Collection,
      Dimensions = request.Dimensions > 0 ? request.Dimensions : 2,
      Limit = request.Limit > 0 ? request.Limit : 2000,
      Level = request.HasLevel ? request.Level : null
    });

    return new GraphSnapshotResponse
    {
      Collection = result.Collection,
      Dimensions = result.Dimensions,
      TotalNodes = result.TotalNodes,
      LiveNodes = result.LiveNodes,
      DeletedNodes = result.DeletedNodes,
      ReturnedNodes = result.ReturnedNodes,
      MaxLevel = result.MaxLevel,
      EntrypointPositionId = result.EntrypointPositionId,
      Truncated = result.Truncated,
      Nodes = { result.Nodes.Select(n => new GraphNodeResponse
      {
        PositionId = n.PositionId,
        Key = n.Key,
        MaxLevel = n.MaxLevel,
        IsDeleted = n.IsDeleted,
        Degree = n.Degree,
        Position = { n.Position }
      }) },
      Edges = { result.Edges.Select(e => new GraphEdgeResponse
      {
        Source = e.Source,
        Target = e.Target,
        Level = e.Level
      }) }
    };
  }

  public override async Task<SearchVectorResponse> SearchFilter(SearchFilterRequest request, ServerCallContext context)
  {
    // Filter-only search: dispatches a SearchVector query with no embeddings.
    // The handler treats an empty/null Embeddings array as "enumerate with filter only,
    // no similarity scoring" and returns all matching entries with Score = 0.
    var result = await mediator.Send(ApplyTuning(new Core.Query.collections.SearchVector
    {
      Database = request.Database,
      Collection = request.Collection,
      Embeddings = [],
      Top = int.MaxValue,
      Filter = ToFilterExpression(request.Filter)
    }, null), context.CancellationToken);

    return ToSearchResponse(result);
  }

  public override async Task<Result> AppendDocument(Document request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.AppendDocument()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Sentence = request.Sentence,
      SparseTerms = ToSparseTerms(request.Sparse),
      SparseFromSentence = request.SparseFromSentence
    });

    return new Result() { Success = true };
  }

  public override async Task<Result> AppendVector(Vector request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.AppendVector()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Vectors = RequireVectors(request),
      SparseVectors = ToSparseVectors(request)
    });

    return new Result() { Success = true };
  }

  public override async Task<Result> SetRawVector(Vector request, ServerCallContext context)
  {
    await mediator.Send(new Core.Command.collections.SetRawVector()
    {
      Database = request.Database,
      Collection = request.Collection,
      Key = request.Key.ToByteArray(),
      Content = request.Content.ToByteArray(),
      Vectors = RequireVectors(request),
      SparseVectors = ToSparseVectors(request)
    });

    return new Result() { Success = true };
  }

  public override async Task Transaction(
    IAsyncStreamReader<TransactionOp> requestStream,
    IServerStreamWriter<TransactionResult> responseStream,
    ServerCallContext context)
  {
    // Placeholder: full Store-level transaction support (WAL-based atomic
    // multi-entry transactions) will be implemented in a follow-up phase
    // once the ITransactionManager abstraction is in place.
    throw new RpcException(new Status(StatusCode.Unimplemented,
      "Transaction RPC is not yet implemented. Full WAL-based transaction support coming in a follow-up phase."));
  }

  // ── Response mappers ──

  private static CollectionInfoResponse ToCollectionInfoResponse(DataStructures.CollectionInfo info)
  {
    var response = new CollectionInfoResponse
    {
      Name = info.Name,
      Vectors = info.Vectors,
      Dimensions = info.Dimensions,
      ContentSize = info.ContentSize,
      VectorSize = info.VectorSize
    };

    if (info.Index is not null)
    {
      response.Index = new CollectionIndexInfoResponse
      {
        IndexSizeBytes = info.Index.IndexSizeBytes,
        Nodes = info.Index.Nodes,
        DeletedNodes = info.Index.DeletedNodes,
        MaxLevel = info.Index.MaxLevel,
        AverageDegree = info.Index.AverageDegree,
        Quantization = info.Index.Quantization
      };
      response.Index.NodesPerLevel.AddRange(info.Index.NodesPerLevel);
    }

    if (info.Sparse is not null)
      response.Sparse = new SparseCollectionInfoResponse
      {
        Segments = info.Sparse.Segments,
        Terms = info.Sparse.Terms,
        Blocks = info.Sparse.Blocks,
        BlockSize = info.Sparse.BlockSize,
        Tombstones = info.Sparse.Tombstones,
        SizeBytes = info.Sparse.SizeBytes
      };

    return response;
  }
}
