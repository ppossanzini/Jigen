using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("database")]
public class DatabaseController(IMediator mediator) : ControllerBase
{
  [HttpPost]
  public async Task<IActionResult> Create(string name)
  {
    await mediator.Send(new Jigen.Core.Command.database.CreateDatabase()
    {
      Name = name
    });
    return Ok();
  }

  [HttpDelete]
  public async Task<IActionResult> Delete(string name)
  {
    await mediator.Send(new Jigen.Core.Command.database.DeleteDatabase()
    {
      Name = name
    });
    return Ok();
  }

  [HttpGet]
  public async Task<IActionResult> List()
  {
    var result = await mediator.Send(new Jigen.Core.Query.database.ListDatabases()
    {
    });
    return Ok(result);
  }
}