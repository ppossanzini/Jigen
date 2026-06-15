using Hikyaku;
using Jigen.Core.Dto.identity;

namespace Jigen.Core.Command.identity;

public class CreateClient : IRequest<CreateClientResult>
{
  public CreateClientData Data { get; set; }
}
