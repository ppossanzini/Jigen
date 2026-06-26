using Grpc.Core;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;

namespace JigenClientTest;

public class VectorCollectionValidationTests
{
  [Fact]
  public void Add_throws_clear_error_when_database_name_is_missing()
  {
    var invoker = new FakeCallInvoker();

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = context.Collection<ValidationEntity>();

    var exception = Assert.Throws<InvalidOperationException>(() =>
      sut.Add(1, new VectorEntry<ValidationEntity>
      {
        Key = 1,
        Content = new ValidationEntity { Name = "ok" },
        Embedding = Array.Empty<float>()
      }));

    Assert.Contains("DatabaseName", exception.Message);
    Assert.DoesNotContain("Value cannot be null", exception.Message);
  }

  [Fact]
  public void Add_throws_argument_null_when_value_is_null()
  {
    var invoker = new FakeCallInvoker();

    var context = new TestContext(
      new ConnectionOptions { HostName = "localhost", Port = 3223, TLS = false, DatabaseName = "TestDb" },
      new StoreCollectionService.StoreCollectionServiceClient(invoker));

    var sut = context.Collection<ValidationEntity>();

    var exception = Assert.Throws<ArgumentNullException>(() => sut.Add(1, null));

    Assert.Equal("value", exception.ParamName);
  }

  private sealed class TestContext : Context
  {
    public TestContext(ConnectionOptions options, StoreCollectionService.StoreCollectionServiceClient serviceClient)
      : base(options, serviceClient)
    {
    }
  }

  private sealed class ValidationEntity
  {
    public string Name { get; set; }
  }

  private sealed class FakeCallInvoker : CallInvoker
  {
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host,
      CallOptions options, TRequest request)
    {
      throw new InvalidOperationException($"Unexpected RPC method: {method.Name}");
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
