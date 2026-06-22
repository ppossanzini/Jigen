using Hikyaku;
using Jigen.Metrics.Core.Dto;
using Jigen.Metrics.Core.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.Metrics.Controllers;

[ApiController]
[Route("metric/server-status")]
public class ServerStatusController(IHikyaku hikyaku) : ControllerBase
{
  [HttpGet("1m")]
  [ProducesResponseType(typeof(ServerStatusHistory), StatusCodes.Status200OK)]
  public Task<IActionResult> GetLastMinute(CancellationToken cancellationToken)
  {
    return GetByWindow(TimeSpan.FromMinutes(1), cancellationToken);
  }

  [HttpGet("5m")]
  [ProducesResponseType(typeof(ServerStatusHistory), StatusCodes.Status200OK)]
  public Task<IActionResult> GetLastFiveMinutes(CancellationToken cancellationToken)
  {
    return GetByWindow(TimeSpan.FromMinutes(5), cancellationToken);
  }

  [HttpGet("10m")]
  [ProducesResponseType(typeof(ServerStatusHistory), StatusCodes.Status200OK)]
  public Task<IActionResult> GetLastTenMinutes(CancellationToken cancellationToken)
  {
    return GetByWindow(TimeSpan.FromMinutes(10), cancellationToken);
  }

  [HttpGet("1h")]
  [ProducesResponseType(typeof(ServerStatusHistory), StatusCodes.Status200OK)]
  public Task<IActionResult> GetLastHour(CancellationToken cancellationToken)
  {
    return GetByWindow(TimeSpan.FromHours(1), cancellationToken);
  }

  private async Task<IActionResult> GetByWindow(TimeSpan requestedWindow, CancellationToken cancellationToken)
  {
    var result = await hikyaku.Send(new GetServerStatusHistory
    {
      Window = requestedWindow
    }, cancellationToken);

    return Ok(result);
  }
}