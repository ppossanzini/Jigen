namespace Jigen.Core.Dto.identity;

public class CreateClientResult : IdentityCommandResult
{
  public string ClientId { get; set; }
  public string ClientSecret { get; set; }
}
