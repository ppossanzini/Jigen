

namespace Jigen.Identity.Core.Dto;

public class CreateClientResult : IdentityCommandResult
{
  public string ClientId { get; set; }
  public string ClientSecret { get; set; }
}
