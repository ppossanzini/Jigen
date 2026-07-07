namespace Jigen.Client;

/// <summary>
/// Server-side failure whose original exception type could not be rebuilt on
/// the client. <see cref="ServerExceptionType"/> carries the original
/// fully-qualified type name as reported by the server.
/// </summary>
public class JigenServerException(string serverExceptionType, string message) : Exception(message)
{
  public string ServerExceptionType { get; } = serverExceptionType;
}
