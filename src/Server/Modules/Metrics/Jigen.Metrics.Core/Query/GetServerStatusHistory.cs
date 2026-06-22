using Hikyaku;
using Jigen.Metrics.Core.Dto;

namespace Jigen.Metrics.Core.Query
{
  public class GetServerStatusHistory : IRequest<ServerStatusHistory>
  {
    public TimeSpan Window { get; set; }
  }
}