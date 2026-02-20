using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Jigen.Proto;

// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Jigen.Client;

public class Context
{
  private readonly GrpcChannel _channel;

  public StoreCollectionService.StoreCollectionServiceClient ServiceClient { get; }
  public ConnectionOptions Options { get; }

  public Context(ConnectionOptions options)
  {
    Options = options;
    _channel = GrpcChannel.ForAddress(options.ConnectionString, options.ChannelOptions);
    // var invoker = _channel.Intercept(new Interceptors.GrpcClientExceptionInterceptor());
    // ServiceClient = new StoreCollectionService.StoreCollectionServiceClient(invoker);
    ServiceClient = new StoreCollectionService.StoreCollectionServiceClient(_channel);
    
    this.ContextBuilder();
  }

  protected virtual void ContextBuilder()
  {
    // Class to autocreate Collections and inject dependency inside collections
  }
}