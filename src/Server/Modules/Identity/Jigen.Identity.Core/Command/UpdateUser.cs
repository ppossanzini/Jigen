using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class UpdateUser : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
  public UpdateUserData Data { get; set; }
}