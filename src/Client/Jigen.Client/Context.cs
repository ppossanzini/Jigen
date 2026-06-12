using System.Net.Http;
using System.Net.Security;
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
    var address = BuildAddress(options);
    ConfigureChannelOptions(options);
    _channel = GrpcChannel.ForAddress(address, options.ChannelOptions);
    // var invoker = _channel.Intercept(new Interceptors.GrpcClientExceptionInterceptor());
    // ServiceClient = new StoreCollectionService.StoreCollectionServiceClient(invoker);
    ServiceClient = new StoreCollectionService.StoreCollectionServiceClient(_channel);
    
    this.ContextBuilder();
  }

  protected Context(ConnectionOptions options, StoreCollectionService.StoreCollectionServiceClient serviceClient)
  {
    Options = options;
    _channel = null!;
    ServiceClient = serviceClient;

    this.ContextBuilder();
  }

  protected virtual void ContextBuilder()
  {
    // Class to autocreate Collections and inject dependency inside collections
  }

  private static string BuildAddress(ConnectionOptions options)
  {
    var scheme = options.TLS ? "https" : "http";
    return $"{scheme}://{options.HostName}:{options.Port}";
  }

  private static void ConfigureChannelOptions(ConnectionOptions options)
  {
    if (!options.TLS)
      AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

    if (!options.AllowUntrustedServerCertificate)
      return;

    if (options.ChannelOptions.HttpHandler == null)
    {
      options.ChannelOptions.HttpHandler = CreateUntrustedHandler();
      return;
    }

    switch (options.ChannelOptions.HttpHandler)
    {
      case SocketsHttpHandler socketsHandler:
        socketsHandler.SslOptions = CreateUntrustedSslOptions();
        break;
      case HttpClientHandler clientHandler:
        clientHandler.ServerCertificateCustomValidationCallback =
          HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        break;
    }
  }

  private static HttpMessageHandler CreateUntrustedHandler()
  {
    return new SocketsHttpHandler { SslOptions = CreateUntrustedSslOptions() };
  }

  private static SslClientAuthenticationOptions CreateUntrustedSslOptions()
  {
    return new SslClientAuthenticationOptions
    {
      RemoteCertificateValidationCallback = static (_, _, _, _) => true
    };
  }
}
