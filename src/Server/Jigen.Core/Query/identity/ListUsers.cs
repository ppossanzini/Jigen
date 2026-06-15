using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Query.identity;

public class ListUsers : IRequest<IEnumerable<UserSummary>>
{
}
