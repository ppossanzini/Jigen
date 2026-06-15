using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class CreateRole : IRequest<IdentityCommandResult>
{
  public CreateRoleData Data { get; set; }
}