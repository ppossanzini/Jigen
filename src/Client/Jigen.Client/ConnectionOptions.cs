using Grpc.Net.Client;

namespace Jigen.Client;

public class ConnectionOptions
{
  public string ConnectionString { get; set; }
  public GrpcChannelOptions ChannelOptions { get; set; } = new ();

  public string DatabaseName { get; set; }
}