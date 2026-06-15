using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class DeleteRole : IRequest<IdentityCommandResult>
{
  public string Id { get; set; }
}