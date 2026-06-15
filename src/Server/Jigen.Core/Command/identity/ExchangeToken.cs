using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class ExchangeToken : IRequest<ExchangeTokenResult>
{
  public ExchangeTokenData Data { get; set; }
}
