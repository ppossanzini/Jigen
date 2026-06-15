namespace Jigen.Core.Dto.identity;

public class ExchangeTokenResult : IdentityCommandResult
{
  public bool UseAuthenticatedPrincipal { get; set; }
  public string Subject { get; set; }
  public string ClientId { get; set; }
  public string[] Scopes { get; set; }
}
