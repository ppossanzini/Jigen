using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class UpdateUser : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
  public UpdateUserData Data { get; set; }
}