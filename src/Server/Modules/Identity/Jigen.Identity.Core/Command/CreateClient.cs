using Hikyaku;
using Jigen.Identity.Core.Dto;

namespace Jigen.Identity.Core.Command;

public class CreateClient : IRequest<CreateClientResult>
{
  public CreateClientData Data { get; set; }
}
