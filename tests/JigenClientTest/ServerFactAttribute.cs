using System.Net.Sockets;
using Grpc.Core;
using Grpc.Net.Client;
using Jigen.Proto;
using Xunit;

namespace JigenClientTest;

/// <summary>
/// Fact that only runs against a usable server. The tests using it speak the
/// real gRPC surface, so without a server, a provisioned database or the
/// embedding module they can only fail with a transport error: they are skipped
/// instead, with the reason in the test report.
/// </summary>
public sealed class ServerFactAttribute : FactAttribute
{
  /// <param name="port">gRPC port of the server under test.</param>
  /// <param name="database">Database that must exist, when the test needs one.</param>
  /// <param name="embeddings">Whether the test needs the server-side embedding module.</param>
  public ServerFactAttribute(int port = 3223, string database = null, bool embeddings = false)
  {
    Skip = ServerProbe.UnusableReason(port, database, embeddings);
  }
}

/// <summary>Theory counterpart of <see cref="ServerFactAttribute"/>.</summary>
public sealed class ServerTheoryAttribute : TheoryAttribute
{
  public ServerTheoryAttribute(int port = 3223, string database = null, bool embeddings = false)
  {
    Skip = ServerProbe.UnusableReason(port, database, embeddings);
  }
}

internal static class ServerProbe
{
  public static string UnusableReason(int port, string database, bool embeddings)
  {
    if (!IsReachable(port))
      return $"No Jigen server listening on port {port}.";

    var client = CreateClient(port);
    try
    {
      if (database is not null)
        client.Count(new CollectionKey { Database = database, Collection = "__server_probe__" });
    }
    catch (RpcException exception)
    {
      return $"Database '{database}' is not available on port {port}: {exception.Status.Detail}";
    }

    if (embeddings)
    {
      try
      {
        client.CalculateEmbeddings(new EmbeddingRequest { Message = "probe" });
      }
      catch (RpcException exception)
      {
        return $"The embedding module is not available on port {port}: {exception.Status.Detail}";
      }
    }

    return null;
  }

  private static StoreCollectionService.StoreCollectionServiceClient CreateClient(int port) =>
    new(GrpcChannel.ForAddress($"http://localhost:{port}"));

  private static bool IsReachable(int port)
  {
    try
    {
      using var client = new TcpClient();
      return client.ConnectAsync("localhost", port).Wait(TimeSpan.FromMilliseconds(250)) &&
             client.Connected;
    }
    catch (Exception)
    {
      return false;
    }
  }
}
