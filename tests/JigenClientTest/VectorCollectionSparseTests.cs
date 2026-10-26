using Google.Protobuf;
using Grpc.Core;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;
using JigenClientTest.Model;

namespace JigenClientTest;

public class VectorCollectionSparseTests
{
  [Fact]
  public void Add_sends_the_sparse_terms_of_a_one_vector_entry()
  {
    Vector captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (Vector)request;
      return new Result { Success = true };
    });

    var sut = CreateCollection(invoker);

    sut.Add(1, new Entity1 { Title = "one" }, [0.1f, 0.2f], new Dictionary<int, float> { [7] = 1.5f });

    Assert.NotNull(captured);
    Assert.Single(captured.Sparse);
    Assert.Equal(1.5f, captured.Sparse[0].Terms[7]);
  }

  [Fact]
  public void Add_sends_one_sparse_list_per_vector()
  {
    Vector captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (Vector)request;
      return new Result { Success = true };
    });

    var sut = CreateCollection(invoker);

    sut.Add(1, new Entity1 { Title = "one" },
      [new[] { 0.1f }, new[] { 0.2f }],
      new List<IReadOnlyDictionary<int, float>>
      {
        new Dictionary<int, float> { [7] = 1f },
        new Dictionary<int, float> { [9] = 2f }
      });

    Assert.Equal(2, captured.Sparse.Count);
    Assert.Equal(7, captured.Sparse[0].Terms.Keys.Single());
    Assert.Equal(9, captured.Sparse[1].Terms.Keys.Single());
  }

  [Fact]
  public void Add_sends_the_document_sparse_terms()
  {
    Document captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (Document)request;
      return new Result { Success = true };
    });

    var sut = CreateCollection(invoker);

    sut.Add(1, new Entity1 { Title = "one" }, "a sentence", new Dictionary<int, float> { [7] = 2f });

    Assert.NotNull(captured);
    Assert.Equal("a sentence", captured.Sentence);
    Assert.Equal(2f, captured.Sparse.Terms[7]);
  }

  [Fact]
  public void Add_refuses_more_sparse_lists_than_vectors()
  {
    var invoker = new FakeCallInvoker((_, _) => new Result { Success = true });
    var sut = CreateCollection(invoker);

    IReadOnlyList<float[]> embeddings = [[0.1f]];
    IReadOnlyList<IReadOnlyDictionary<int, float>> sparse =
    [
      new Dictionary<int, float> { [7] = 1f },
      new Dictionary<int, float> { [9] = 1f }
    ];

    Assert.Throws<ArgumentException>(() => sut.Add(1, new Entity1 { Title = "one" }, embeddings, sparse));
  }

  [Fact]
  public void SearchSparse_sends_the_sparse_query()
  {
    SearchVectorRequest captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (SearchVectorRequest)request;
      return new SearchVectorResponse
      {
        Results =
        {
          new SearchVectorResult
          {
            Key = ByteString.CopyFrom(BitConverter.GetBytes(4)),
            Content = ByteString.CopyFrom(
              MessagePackDocumentSerializer.Instance.Serialize(new Entity1 { Title = "hit" }).Span),
            Score = 1.5f,
            SparseScore = 1.5f
          }
        }
      };
    });

    var sut = CreateCollection(invoker);

    var results = sut.SearchSparse(new Dictionary<int, float> { [7] = 1f }, top: 3);

    Assert.Equal(3, captured.Top);
    Assert.Empty(captured.Embeddings);
    Assert.Equal(1f, captured.SparseQuery[7]);
    Assert.Single(results);
    Assert.Equal(1.5f, results[0].SparseScore);
    Assert.Null(results[0].DenseScore);
  }

  [Fact]
  public void Search_sends_both_halves_and_the_score_cuts()
  {
    SearchVectorRequest captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (SearchVectorRequest)request;
      return new SearchVectorResponse
      {
        Results =
        {
          new SearchVectorResult
          {
            Key = ByteString.CopyFrom(BitConverter.GetBytes(4)),
            Content = ByteString.CopyFrom(
              MessagePackDocumentSerializer.Instance.Serialize(new Entity1 { Title = "hit" }).Span),
            Score = 0.9f,
            DenseScore = 0.8f,
            SparseScore = 1.5f
          }
        }
      };
    });

    var sut = CreateCollection(invoker);

    var results = sut.Search([0.1f, 0.2f], new Dictionary<int, float> { [7] = 1f },
      top: 5, options: new SearchOptions { MinDenseScore = 0.3f, MinSparseScore = 0.1f });

    Assert.Equal(2, captured.Embeddings.Count);
    Assert.Equal(1f, captured.SparseQuery[7]);
    Assert.Equal(0.3f, captured.Tuning.MinDenseScore);
    Assert.Equal(0.1f, captured.Tuning.MinSparseScore);
    Assert.Equal(0.9f, results[0].Score);
    Assert.Equal(0.8f, results[0].DenseScore);
    Assert.Equal(1.5f, results[0].SparseScore);
  }

  [Fact]
  public void Search_from_a_sentence_asks_the_server_for_the_sparse_half()
  {
    SearchDocumentRequest captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (SearchDocumentRequest)request;
      return new SearchVectorResponse
      {
        Results =
        {
          new SearchVectorResult
          {
            Key = ByteString.CopyFrom(BitConverter.GetBytes(5)),
            Content = ByteString.CopyFrom(
              MessagePackDocumentSerializer.Instance.Serialize(new Entity1 { Title = "hit" }).Span),
            Score = 1.2f,
            SparseScore = 1.2f
          }
        }
      };
    });

    var sut = CreateCollection(invoker);

    var results = sut.Search("hybrid from text", top: 4, sparseFromSentence: true);

    Assert.Equal("hybrid from text", captured.Sentence);
    Assert.Equal(4, captured.Top);
    Assert.True(captured.SparseFromSentence);
    Assert.Empty(captured.SparseQuery);
    Assert.Equal(1.2f, results[0].SparseScore);
  }

  [Fact]
  public void Add_can_ask_the_server_to_derive_the_sparse_terms_from_the_sentence()
  {
    Document captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (Document)request;
      return new Result { Success = true };
    });

    CreateCollection(invoker).Add(1, new Entity1 { Title = "one" }, "a sentence", sparseFromSentence: true);

    Assert.NotNull(captured);
    Assert.Equal("a sentence", captured.Sentence);
    Assert.True(captured.SparseFromSentence);
  }

  [Fact]
  public void SearchDense_sends_the_dense_score_cut()
  {
    SearchVectorRequest captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (SearchVectorRequest)request;
      return new SearchVectorResponse();
    });

    CreateCollection(invoker).SearchDense([0.1f], top: 3, options: new SearchOptions { MinDenseScore = 0.4f });

    Assert.Equal(0.4f, captured.Tuning.MinDenseScore);
    Assert.Equal(0f, captured.Tuning.MinSparseScore);
  }

  [Fact]
  public void SearchSparse_sends_the_sparse_score_cut()
  {
    SearchVectorRequest captured = null;

    var invoker = new FakeCallInvoker((_, request) =>
    {
      captured = (SearchVectorRequest)request;
      return new SearchVectorResponse();
    });

    CreateCollection(invoker).SearchSparse(new Dictionary<int, float> { [7] = 1f },
      options: new SearchOptions { MinSparseScore = 0.2f });

    Assert.Equal(0.2f, captured.Tuning.MinSparseScore);
  }

  [Fact]
  public void Search_without_any_half_is_refused()
  {
    var invoker = new FakeCallInvoker((_, _) => new SearchVectorResponse());
    var sut = CreateCollection(invoker);

    Assert.Throws<ArgumentException>(() => sut.Search());
  }

  private static VectorCollection<Entity1> CreateCollection(CallInvoker invoker)
  {
    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    return new VectorCollection<Entity1>(context);
  }

  private sealed class TestContext : Context
  {
    public TestContext(ConnectionOptions options, StoreCollectionService.StoreCollectionServiceClient serviceClient)
      : base(options, serviceClient)
    {
    }
  }

  private sealed class FakeCallInvoker(Func<string, object, object> dispatcher) : CallInvoker
  {
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host,
      CallOptions options, TRequest request) =>
      (TResponse)dispatcher(method.Name, request);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
      string host, CallOptions options, TRequest request) =>
      throw new NotImplementedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) =>
      throw new NotImplementedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options) =>
      throw new NotImplementedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
      Method<TRequest, TResponse> method, string host, CallOptions options) =>
      throw new NotImplementedException();
  }
}
