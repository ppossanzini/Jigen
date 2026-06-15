using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class Login : IRequest<IdentityCommandResult>
{
  public LoginData Data { get; set; }
}
