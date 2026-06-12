using Grpc.Core;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;
using JigenClientTest.Model;

namespace JigenClientTest;

public class VectorCollectionSearchTests
{
  [Fact]
  public void Search_with_embeddings_uses_search_vector_rpc()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method == "SearchVector")
      {
        var searchRequest = Assert.IsType<SearchVectorRequest>(request);
        Assert.Equal("TestDb", searchRequest.Database);
        Assert.Equal(2, searchRequest.Top);
        Assert.Equal(3, searchRequest.Embeddings.Count);

        return new SearchVectorResponse
        {
          Results =
          {
            new SearchVectorResult
            {
              Key = Google.Protobuf.ByteString.CopyFrom(BitConverter.GetBytes(7)),
              Content = Google.Protobuf.ByteString.CopyFrom(
                MessagePackDocumentSerializer.Instance.Serialize(new Entity1
                {
                  Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                  Title = "hello",
                  Sentence = "world"
                }).Span),
              Score = 0.95f
            }
          }
        };
      }

      throw new InvalidOperationException($"Unexpected RPC method: {method}");
    });

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = new VectorCollection<Entity1>(context);

    var results = sut.Search([0.1f, 0.2f, 0.3f], top: 2);

    Assert.Single(results);
    Assert.Equal(7, BitConverter.ToInt32(results[0].Key.Value));
    Assert.Equal("hello", results[0].Content.Title);
    Assert.Equal(0.95f, results[0].Score);
    Assert.Contains("SearchVector", invoker.CalledMethods);
  }

  [Fact]
  public void Search_with_sentence_uses_search_document_rpc()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method == "SearchDocument")
      {
        var searchRequest = Assert.IsType<SearchDocumentRequest>(request);
        Assert.Equal("Find me", searchRequest.Sentence);
        Assert.Equal(3, searchRequest.Top);

        return new SearchVectorResponse
        {
          Results =
          {
            new SearchVectorResult
            {
              Key = Google.Protobuf.ByteString.CopyFrom(BitConverter.GetBytes(3)),
              Content = Google.Protobuf.ByteString.CopyFrom(
                MessagePackDocumentSerializer.Instance.Serialize(new Entity1
                {
                  Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                  Title = "from sentence",
                  Sentence = "Find me"
                }).Span),
              Score = 0.88f
            }
          }
        };
      }

      throw new InvalidOperationException($"Unexpected RPC method: {method}");
    });

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = new VectorCollection<Entity1>(context);

    var results = sut.Search("Find me", top: 3);

    Assert.Single(results);
    Assert.Equal("from sentence", results[0].Content.Title);
    Assert.Contains("SearchDocument", invoker.CalledMethods);
    Assert.DoesNotContain("CalculateEmbeddings", invoker.CalledMethods);
  }

  [Fact]
  public void Search_with_sentence_and_expression_sends_filter_ast()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method == "SearchDocument")
      {
        var searchRequest = Assert.IsType<SearchDocumentRequest>(request);
        Assert.NotNull(searchRequest.Filter);
        Assert.Equal(FilterNode.KindOneofCase.And, searchRequest.Filter.KindCase);

        Assert.Equal(FilterNode.KindOneofCase.CollectionAny, searchRequest.Filter.And.Left.KindCase);
        Assert.Equal("Tags", searchRequest.Filter.And.Left.CollectionAny.PropertyPath);
        Assert.Equal(FilterValue.KindOneofCase.StringValue, searchRequest.Filter.And.Left.CollectionAny.Value.KindCase);
        Assert.Equal("science", searchRequest.Filter.And.Left.CollectionAny.Value.StringValue);

        Assert.Equal(FilterNode.KindOneofCase.Equals_, searchRequest.Filter.And.Right.KindCase);
        Assert.Equal("Metadata.Country", searchRequest.Filter.And.Right.Equals_.PropertyPath);
        Assert.Equal("IT", searchRequest.Filter.And.Right.Equals_.Value.StringValue);

        return new SearchVectorResponse();
      }

      throw new InvalidOperationException($"Unexpected RPC method: {method}");
    });

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = new VectorCollection<SearchEntity>(context);

    _ = sut.Search("Find me", x => x.Tags.Any(tag => tag == "science") && x.Metadata.Country == "IT", top: 4);

    Assert.Contains("SearchDocument", invoker.CalledMethods);
  }

  private sealed class TestContext : Context
  {
    public TestContext(ConnectionOptions options, StoreCollectionService.StoreCollectionServiceClient serviceClient)
      : base(options, serviceClient)
    {
    }
  }

  public sealed class SearchEntity
  {
    public List<string> Tags { get; set; }
    public SearchMetadata Metadata { get; set; }
  }

  public sealed class SearchMetadata
  {
    public string Country { get; set; }
  }

  private sealed class FakeCallInvoker(Func<string, object, object> dispatcher) : CallInvoker
  {
    public List<string> CalledMethods { get; } = [];

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host,
      CallOptions options, TRequest request)
    {
      CalledMethods.Add(method.Name);
      return (TResponse)dispatcher(method.Name, request);
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
      string host, CallOptions options, TRequest request)
    {
      throw new NotImplementedException();
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
    {
      throw new NotImplementedException();
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options)
    {
      throw new NotImplementedException();
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options)
    {
      throw new NotImplementedException();
    }
  }
}
