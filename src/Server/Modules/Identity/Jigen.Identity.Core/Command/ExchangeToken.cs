using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class ExchangeToken : IRequest<ExchangeTokenResult>
{
  public ExchangeTokenData Data { get; set; }
}
