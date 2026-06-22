using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class Login : IRequest<IdentityCommandResult>
{
  public LoginData Data { get; set; }
}
