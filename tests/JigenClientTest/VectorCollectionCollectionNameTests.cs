using Grpc.Core;
using Jigen.Client;
using Jigen.Proto;

namespace JigenClientTest;

public class VectorCollectionCollectionNameTests
{
  [Fact]
  public void Count_uses_collection_name_from_type()
  {
    var invoker = new FakeCallInvoker((method, request) =>
    {
      if (method == "Count")
      {
        var collectionKey = Assert.IsType<CollectionKey>(request);
        Assert.Equal("TestDb", collectionKey.Database);
        Assert.Equal(nameof(CollectionNameEntity), collectionKey.Collection);
        Assert.NotEqual("T", collectionKey.Collection);

        return new CountResult { Count = 0 };
      }

      throw new InvalidOperationException($"Unexpected RPC method: {method}");
    });

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = context.Collection<CollectionNameEntity>();

    _ = sut.Count;

    Assert.Contains("Count", invoker.CalledMethods);
  }

  private sealed class TestContext : Context
  {
    public TestContext(ConnectionOptions options, StoreCollectionService.StoreCollectionServiceClient serviceClient)
      : base(options, serviceClient)
    {
    }
  }

  private sealed class CollectionNameEntity
  {
    public string Name { get; set; }
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
