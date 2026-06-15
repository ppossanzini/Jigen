namespace Jigen.Identity.Core.Dto;

public class ExchangeTokenData
{
  public string GrantType { get; set; }
  public string ClientId { get; set; }
  public string[] Scopes { get; set; }
}
