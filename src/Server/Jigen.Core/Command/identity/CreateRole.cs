using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class CreateRole : IRequest<IdentityCommandResult>
{
  public CreateRoleData Data { get; set; }
}