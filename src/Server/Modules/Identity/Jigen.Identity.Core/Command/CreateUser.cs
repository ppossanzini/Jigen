using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class CreateUser : IRequest<IdentityCommandResult>
{
  public CreateUserData Data { get; set; }
}