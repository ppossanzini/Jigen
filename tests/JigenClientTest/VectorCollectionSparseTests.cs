using Google.Protobuf;
using Grpc.Core;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;
using JigenClientTest.Model;
using SparseVector = Jigen.Client.BaseTypes.SparseVector;

namespace JigenClientTest;

/// <summary>
/// The sparse slot from the typed client: one write carrying both vectors, the
/// read-back, the exact lexical search and the fusion. The fake service records
/// the requests, so the assertions are on what leaves the client, not on what
/// the server would do with it.
/// </summary>
public class VectorCollectionSparseTests
{
  private static SparseVector Vector(int[] terms, float[] weights) => SparseVector.Create(terms, weights);

  private static ByteString Content(string title) => ByteString.CopyFrom(
    MessagePackDocumentSerializer.Instance.Serialize(new Entity1
    {
      Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
      Title = title,
      Sentence = title
    }).Span);

  private static VectorCollection<Entity1> Collection(FakeCallInvoker invoker) =>
    new(new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker)));

  // ── Write ──

  [Fact]
  public void Add_sends_dense_and_sparse_in_one_call()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      Assert.Equal("SetVector", method);

      var vector = Assert.IsType<Vector>(request);
      Assert.Equal(new[] { 0.1f, 0.2f }, vector.Embeddings.ToArray());
      Assert.Equal(new[] { 12, 98 }, vector.Sparse.Terms.ToArray());
      Assert.Equal(new[] { 0.8f, 1.2f }, vector.Sparse.Weights.ToArray());

      return new Result { Success = true };
    });

    Collection(invoker).Add(1, new VectorEntry<Entity1>
    {
      Key = 1,
      Content = new Entity1 { Title = "doc" },
      Embedding = [0.1f, 0.2f],
      Sparse = Vector([12, 98], [0.8f, 1.2f])
    });

    // One insertion, not two: the slots always describe the same revision.
    Assert.Single(invoker.CalledMethods);
  }

  [Fact]
  public void Add_with_only_the_sparse_vector_sends_no_embeddings()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      var vector = Assert.IsType<Vector>(request);
      Assert.Empty(vector.Embeddings);
      Assert.Equal(new[] { 7 }, vector.Sparse.Terms.ToArray());
      return new Result { Success = true };
    });

    Collection(invoker).Add(2, new VectorEntry<Entity1>
    {
      Key = 2,
      Content = new Entity1 { Title = "lexical only" },
      Sparse = Vector([7], [1f])
    });

    Assert.Contains("SetVector", invoker.CalledMethods);
  }

  [Fact]
  public void Add_without_sparse_sends_no_sparse_message()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      var vector = Assert.IsType<Vector>(request);
      Assert.Null(vector.Sparse);
      return new Result { Success = true };
    });

    Collection(invoker).Add(3, new VectorEntry<Entity1>
    {
      Key = 3,
      Content = new Entity1 { Title = "dense only" },
      Embedding = [0.5f]
    });

    Assert.Contains("SetVector", invoker.CalledMethods);
  }

  [Fact]
  public void AddAsync_overload_accepts_both_vectors_together()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      var vector = Assert.IsType<Vector>(request);
      Assert.Single(vector.Embeddings);
      Assert.Equal(new[] { 3, 9 }, vector.Sparse.Terms.ToArray());
      return new Result { Success = true };
    });

    Collection(invoker).AddAsync(4, new Entity1 { Title = "both" }, [0.5f], Vector([3, 9], [1f, 2f]))
      .GetAwaiter().GetResult();

    Assert.Contains("SetVector", invoker.CalledMethods);
  }

  // ── Read ──

  [Fact]
  public void GetSparse_returns_the_stored_pairs()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      Assert.Equal("GetSparse", method);
      var message = new Jigen.Proto.SparseVector();
      message.Terms.AddRange([2, 9]);
      message.Weights.AddRange([0.25f, 3f]);
      return message;
    });

    var sparse = Collection(invoker).GetSparse(5);

    Assert.NotNull(sparse);
    Assert.Equal(new[] { 2, 9 }, sparse.Terms.ToArray());
    Assert.Equal(new[] { 0.25f, 3f }, sparse.Weights.ToArray());
  }

  [Fact]
  public void GetSparse_returns_null_when_the_entry_has_no_slot()
  {
    var invoker = new FakeCallInvoker((method, request) => new Jigen.Proto.SparseVector());

    Assert.Null(Collection(invoker).GetSparse(6));
  }

  // ── Search ──

  [Fact]
  public void Search_with_a_sparse_vector_uses_the_lexical_rpc()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      Assert.Equal("SearchSparse", method);

      var searchRequest = Assert.IsType<SearchSparseRequest>(request);
      Assert.Equal("TestDb", searchRequest.Database);
      Assert.Equal(new[] { 1, 5 }, searchRequest.Sparse.Terms.ToArray());

      return new SearchVectorResponse
      {
        Results =
        {
          new SearchVectorResult
          {
            Key = ByteString.CopyFrom(BitConverter.GetBytes(9)),
            Content = Content("lexical"),
            Score = 4f,
            InSparse = true,
            SparseScore = 4f
          }
        }
      };
    });

    var results = Collection(invoker).Search(Vector([1, 5], [1f, 2f]), top: 5);

    var hit = Assert.Single(results);
    Assert.Equal(4f, hit.Score);
    Assert.True(hit.InSparse);
    Assert.False(hit.InDense);
    Assert.Equal(4f, hit.SparseScore);
  }

  [Fact]
  public void Search_hybrid_uses_the_fusion_rpc_and_reports_provenance()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      Assert.Equal("SearchHybrid", method);

      var searchRequest = Assert.IsType<SearchHybridRequest>(request);
      Assert.Single(searchRequest.Embeddings);
      Assert.Equal(new[] { 1 }, searchRequest.Sparse.Terms.ToArray());
      Assert.True(searchRequest.HasDenseWeight);
      Assert.True(searchRequest.HasSparseWeight);

      return new SearchVectorResponse
      {
        Results =
        {
          new SearchVectorResult
          {
            Key = ByteString.CopyFrom(BitConverter.GetBytes(11)),
            Content = Content("fused"),
            Score = 0.033f,
            InDense = true,
            InSparse = true,
            DenseScore = 0.9f,
            SparseScore = 3f
          }
        }
      };
    });

    var results = Collection(invoker).SearchHybrid([0.4f], Vector([1], [1f]), top: 5, denseWeight: 2f, sparseWeight: 1f);

    var hit = Assert.Single(results);
    Assert.True(hit.InDense);
    Assert.True(hit.InSparse);
    Assert.Equal(0.9f, hit.DenseScore);
    Assert.Equal(3f, hit.SparseScore);
  }

  [Fact]
  public void SearchHybrid_from_a_sentence_sends_no_vectors()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      Assert.Equal("SearchHybrid", method);

      var searchRequest = Assert.IsType<SearchHybridRequest>(request);
      Assert.Equal("il parco è buio", searchRequest.Sentence);
      Assert.Empty(searchRequest.Embeddings);
      Assert.Null(searchRequest.Sparse);
      Assert.True(searchRequest.HasDenseWeight);
      Assert.True(searchRequest.HasSparseWeight);

      return new SearchVectorResponse();
    });

    Collection(invoker).SearchHybrid("il parco è buio", top: 5);

    Assert.Contains("SearchHybrid", invoker.CalledMethods);
  }

  [Fact]
  public void SearchHybrid_with_an_empty_sentence_is_rejected_before_the_call()
  {
    var invoker = new FakeCallInvoker((method, request) =>
      throw new InvalidOperationException($"Unexpected RPC method: {method}"));

    Assert.Throws<ArgumentException>(() => Collection(invoker).SearchHybrid("   ", top: 5));
    Assert.Empty(invoker.CalledMethods);
  }

  [Fact]
  public void Search_hybrid_without_any_vector_is_rejected_before_the_call()
  {
    var invoker = new FakeCallInvoker((method, request) =>
      throw new InvalidOperationException($"Unexpected RPC method: {method}"));

    var exception = Assert.Throws<ArgumentException>(() =>
      Collection(invoker).SearchHybrid(Array.Empty<float>(), null, top: 5));

    Assert.Contains("at least one", exception.Message);
    Assert.Empty(invoker.CalledMethods);
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
      CalledMethods.Add(method.Name);
      var response = (TResponse)dispatcher(method.Name, request);
      return new AsyncUnaryCall<TResponse>(
        Task.FromResult(response),
        Task.FromResult(new Metadata()),
        () => Status.DefaultSuccess,
        () => new Metadata(),
        () => { });
    }

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
