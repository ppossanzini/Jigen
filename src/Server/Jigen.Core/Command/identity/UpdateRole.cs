using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class UpdateRole : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
  public UpdateRoleData Data { get; set; }
}