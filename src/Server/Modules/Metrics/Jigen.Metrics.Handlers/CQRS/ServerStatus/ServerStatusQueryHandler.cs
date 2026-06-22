using Hikyaku;
using Jigen.Metrics.Core.Dto;
using Jigen.Metrics.Core.Query;
using Jigen.Metrics.Handlers.Services;

namespace Jigen.Metrics.Handlers.CQRS.ServerStatus;

public class ServerStatusQueryHandler(ServerStatusHistoryService historyService)
  : IRequestHandler<GetServerStatusHistory, ServerStatusHistory>
{
  public Task<ServerStatusHistory> Handle(GetServerStatusHistory request, CancellationToken cancellationToken)
  {
    return historyService.GetHistoryAsync(request.Window, cancellationToken);
  }
}