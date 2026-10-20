using Google.Protobuf;
using Grpc.Core;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;

namespace JigenClientTest;

/// <summary>
/// Shared metadata over the .NET client: the metadata index is opaque and
/// stable, the association is collection-scoped and the grouped search budget
/// is mandatory.
/// </summary>
public class VectorCollectionMetadataTests
{
  private static VectorCollection<MetadataEntity> CreateSut(FakeCallInvoker invoker) =>
    new(new TestContext(
        new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
        new StoreCollectionService.StoreCollectionServiceClient(invoker)),
      new VectorCollectionOptions<MetadataEntity> { Name = "articles" });

  [Fact]
  public void CreateMetadata_returns_index_and_uses_database_scope()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "CreateMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var create = Assert.IsType<CreateMetadataRequest>(request);
      Assert.Equal("TestDb", create.Database);
      Assert.Equal("shared", MessagePackDocumentSerializer.Instance
        .Deserialize<MetadataEntity>(create.Content.Memory).Title);

      return new CreateMetadataResponse { MetadataIndex = 9 };
    });

    Assert.Equal(9, CreateSut(invoker).CreateMetadata(new MetadataEntity { Title = "shared" }));
  }

  [Fact]
  public void UpdateMetadata_reports_missing_index()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "UpdateMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var update = Assert.IsType<UpdateMetadataRequest>(request);
      return new UpdateMetadataResponse { Updated = update.MetadataIndex == 9 };
    });

    var sut = CreateSut(invoker);

    Assert.True(sut.UpdateMetadata(9, new MetadataEntity { Title = "new" }));
    Assert.False(sut.UpdateMetadata(10, new MetadataEntity { Title = "new" }));
  }

  [Fact]
  public void GetMetadataIndex_sends_collection_and_key()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "GetEmbeddingMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var query = Assert.IsType<EmbeddingMetadataRequest>(request);
      Assert.Equal("TestDb", query.Database);
      Assert.Equal("articles", query.Collection);
      Assert.Equal("key-1"u8.ToArray(), query.Key.ToByteArray());

      return new EmbeddingMetadataResponse { MetadataIndex = 4 };
    });

    Assert.Equal(4, CreateSut(invoker).GetMetadataIndex("key-1"));
  }

  [Fact]
  public void AssociateMetadata_sends_target_index()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "AssociateEmbedding")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var associate = Assert.IsType<AssociateEmbeddingRequest>(request);
      Assert.Equal(12, associate.MetadataIndex);
      Assert.Equal("key-1"u8.ToArray(), associate.Key.ToByteArray());

      return new Result { Success = true };
    });

    Assert.True(CreateSut(invoker).AssociateMetadata("key-1", 12));
  }

  [Fact]
  public void SearchByMetadata_maps_groups_and_sends_budget()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "SearchByMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var search = Assert.IsType<SearchByMetadataRequest>(request);
      Assert.Equal(3, search.Top);
      Assert.Equal(40, search.CandidateBudget);
      Assert.Equal(64, search.Tuning.EfSearch);

      return new SearchByMetadataResponse
      {
        Results =
        {
          new SearchByMetadataResult
          {
            MetadataIndex = 5,
            Key = ByteString.CopyFrom("winner"u8.ToArray()),
            Content = ByteString.CopyFrom(MessagePackDocumentSerializer.Instance
              .Serialize(new MetadataEntity { Title = "shared" }).Span),
            Score = 0.8f
          }
        }
      };
    });

    var result = Assert.Single(CreateSut(invoker)
      .SearchByMetadata([0.1f], top: 3, candidateBudget: 40, new SearchOptions { EfSearch = 64 }));

    Assert.Equal(5, result.MetadataIndex);
    Assert.Equal("winner"u8.ToArray(), result.Key.Value);
    Assert.Equal("shared", result.Content.Title);
    Assert.Equal(0.8f, result.Score);
  }

  [Fact]
  public void SearchByMetadata_throws_on_non_positive_budget()
  {
    var sut = CreateSut(new FakeCallInvoker((method, _) =>
      throw new InvalidOperationException($"Unexpected RPC method: {method}")));

    Assert.Throws<ArgumentOutOfRangeException>(() => sut.SearchByMetadata([0.1f], 3, 0));
  }

  [Fact]
  public void SearchByMetadata_translates_predicate_into_filter_node()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "SearchByMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var search = Assert.IsType<SearchByMetadataRequest>(request);
      Assert.Equal(FilterNode.KindOneofCase.Equals_, search.Filter.KindCase);
      Assert.Equal("Title", search.Filter.Equals_.PropertyPath);
      Assert.Equal("shared", search.Filter.Equals_.Value.StringValue);
      Assert.Equal(20, search.CandidateBudget);

      return new SearchByMetadataResponse();
    });

    Assert.Empty(CreateSut(invoker)
      .SearchByMetadata([0.1f], e => e.Title == "shared", top: 2, candidateBudget: 20));
  }

  [Fact]
  public void SearchByMetadata_from_sentence_translates_predicate()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "SearchByMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var search = Assert.IsType<SearchByMetadataRequest>(request);
      Assert.Equal("carbonara", search.Sentence);
      Assert.Equal(FilterNode.KindOneofCase.Equals_, search.Filter.KindCase);
      Assert.Equal("Title", search.Filter.Equals_.PropertyPath);

      return new SearchByMetadataResponse();
    });

    Assert.Empty(CreateSut(invoker)
      .SearchByMetadata("carbonara", e => e.Title == "shared", top: 2, candidateBudget: 20));
  }

  [Fact]
  public void SearchByMetadata_without_predicate_leaves_filter_empty()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "SearchByMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      // The field is left unset, so the server applies no filter.
      var search = Assert.IsType<SearchByMetadataRequest>(request);
      Assert.Null(search.Filter);

      return new SearchByMetadataResponse();
    });

    Assert.Empty(CreateSut(invoker).SearchByMetadata([0.1f], 2, 20));
  }

  [Fact]
  public void SearchByMetadata_from_sentence_sends_sentence_only()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method != "SearchByMetadata")
        throw new InvalidOperationException($"Unexpected RPC method: {method}");

      var search = Assert.IsType<SearchByMetadataRequest>(request);
      Assert.Equal("carbonara", search.Sentence);
      Assert.Empty(search.Embeddings);
      Assert.Equal(10, search.CandidateBudget);

      return new SearchByMetadataResponse();
    });

    Assert.Empty(CreateSut(invoker).SearchByMetadata("carbonara", 5, 10));
  }

  private sealed class TestContext(ConnectionOptions options,
    StoreCollectionService.StoreCollectionServiceClient serviceClient)
    : Context(options, serviceClient);

  private sealed class FakeCallInvoker(Func<string, object, object> dispatcher) : CallInvoker
  {
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host,
      CallOptions options, TRequest request) => (TResponse)dispatcher(method.Name, request);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
      string host, CallOptions options, TRequest request) => throw new NotImplementedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) =>
      throw new NotImplementedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotImplementedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotImplementedException();
  }

  public sealed class MetadataEntity
  {
    public string Title { get; set; }
  }
}
