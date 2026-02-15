using Grpc.Net.Client;
using Jigen.Proto;

// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Jigen.Client;

public class Context
{
  private readonly GrpcChannel _channel;
  private StoreCollectionService.StoreCollectionServiceClient _client;

  public Context(ConnectionOptions options)
  {
    _channel = GrpcChannel.ForAddress(options.ConnectionString, options.ChannelOptions);
    _client = new StoreCollectionService.StoreCollectionServiceClient(_channel);

    this.ContextBuilder();
  }

  protected virtual void ContextBuilder()
  {
    // Class to autocreate Collections and inject dependency inside collections
    
  }
}