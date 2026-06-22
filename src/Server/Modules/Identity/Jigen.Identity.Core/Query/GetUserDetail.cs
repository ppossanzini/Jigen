using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Query;

public class GetUserDetail : IRequest<UserDetail?>
{
  public string Id { get; set; }
}
