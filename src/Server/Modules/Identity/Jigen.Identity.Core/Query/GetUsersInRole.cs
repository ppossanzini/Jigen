using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Query;

public class GetUsersInRole : IRequest<IEnumerable<UserSummary>?>
{
  public string RoleId { get; set; }
}
