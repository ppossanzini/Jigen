using Grpc.Net.Client;

namespace Jigen.Client;

public class ConnectionOptions
{
  public string HostName { get; set; } = "localhost";

  public int Port { get; set; } = 3223;

  public bool TLS { get; set; }

  public bool AllowUntrustedServerCertificate { get; set; }

  public string DatabaseName { get; set; }
}
