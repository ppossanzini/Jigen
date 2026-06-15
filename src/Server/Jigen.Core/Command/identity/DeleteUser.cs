using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class DeleteUser : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
}