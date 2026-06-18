using Hikyaku;
using Jigen.Core.Dto.database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("database")]
public class DatabaseController(IHikyaku mediator) : ControllerBase
{
  [HttpPost]
  public async Task<IActionResult> Create(string name, CancellationToken cancellationToken)
  {
    await mediator.Send(new Jigen.Core.Command.database.CreateDatabase()
    {
      Name = name
    }, cancellationToken);
    return Ok();
  }

  [HttpDelete]
  public async Task<IActionResult> Delete(string name, CancellationToken cancellationToken)
  {
    await mediator.Send(new Jigen.Core.Command.database.DeleteDatabase()
    {
      Name = name
    }, cancellationToken);
    return Ok();
  }

  [HttpGet]
  public async Task<IActionResult> List(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Jigen.Core.Query.database.ListDatabases()
    {
    }, cancellationToken);
    return Ok(result);
  }

  [Authorize(Policy = "database.admin")]
  [HttpGet("{name}/details")]
  [ProducesResponseType(typeof(DatabaseDetails), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetDetails(string name, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Jigen.Core.Query.database.GetDetails
    {
      Database = name
    }, cancellationToken);

    return Ok(result);
  }

  [Authorize(Policy = "database.admin")]
  [HttpGet("{name}/users")]
  [ProducesResponseType(typeof(IEnumerable<DatabaseUserInfo>), StatusCodes.Status200OK)]
  public async Task<IActionResult> ListUsers(string name, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Jigen.Core.Query.database.ListDatabaseUsers
    {
      Database = name
    }, cancellationToken);

    return Ok(result);
  }

  [Authorize(Policy = "database.admin")]
  [HttpPut("{name}/users")]
  [ProducesResponseType(typeof(IEnumerable<DatabaseUserInfo>), StatusCodes.Status200OK)]
  public async Task<IActionResult> SetUsers(string name, [FromBody] SetDatabaseUsersData request, CancellationToken cancellationToken)
  {
    await mediator.Send(new Jigen.Core.Command.database.SetDatabaseUsers
    {
      Database = name,
      Data = request
    }, cancellationToken);

    var result = await mediator.Send(new Jigen.Core.Query.database.ListDatabaseUsers
    {
      Database = name
    }, cancellationToken);

    return Ok(result);
  }
}