using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class CreateUser : IRequest<IdentityCommandResult>
{
  public CreateUserData Data { get; set; }
}