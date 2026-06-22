namespace Jigen.Identity.Core.Dto;

public class CreateClientData
{
  public string ClientId { get; set; }
  public string DisplayName { get; set; }
  public bool AllowAuthorizationCode { get; set; }
  public bool AllowClientCredentials { get; set; }
  public bool AllowRefreshToken { get; set; }
  public string[] RedirectUris { get; set; }
  public string[] PostLogoutRedirectUris { get; set; }
  public string[] Scopes { get; set; }
}
