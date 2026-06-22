namespace Jigen.Identity.Core.Dto;

public class ExchangeTokenResult : IdentityCommandResult
{
  public bool UseAuthenticatedPrincipal { get; set; }
  public string Subject { get; set; }
  public string ClientId { get; set; }
  public string[] Scopes { get; set; }
}
