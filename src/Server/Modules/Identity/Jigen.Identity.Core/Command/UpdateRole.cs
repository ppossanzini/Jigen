using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class UpdateRole : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
  public UpdateRoleData Data { get; set; }
}