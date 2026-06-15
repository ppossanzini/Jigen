namespace Jigen.Core.Dto.identity;

public class ExchangeTokenData
{
  public string GrantType { get; set; }
  public string ClientId { get; set; }
  public string[] Scopes { get; set; }
}
