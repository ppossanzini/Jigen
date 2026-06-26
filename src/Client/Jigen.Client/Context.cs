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
    var channelOptions = ConfigureChannelOptions(options);
    _channel = GrpcChannel.ForAddress(address, channelOptions);
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

  private static GrpcChannelOptions ConfigureChannelOptions(ConnectionOptions options)
  {
    var result = new GrpcChannelOptions();

    if (!options.TLS)
      AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

    if (!options.AllowUntrustedServerCertificate)
      return result;

    if (result.HttpHandler == null)
    {
      result.HttpHandler = CreateUntrustedHandler();
      return result;
    }

    switch (result.HttpHandler)
    {
      case SocketsHttpHandler socketsHandler:
        socketsHandler.SslOptions = CreateUntrustedSslOptions();
        break;
      case HttpClientHandler clientHandler:
        clientHandler.ServerCertificateCustomValidationCallback =
          HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        break;
    }
    return result;
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
