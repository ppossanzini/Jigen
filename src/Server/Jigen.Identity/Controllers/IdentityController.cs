using Hikyaku;
using Jigen.Core.Dto.identity;
using Jigen.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.Identity.Controllers;

[ApiController]
public class IdentityController(IHikyaku mediator) : ControllerBase
{
  [HttpPost("~/identity/login")]
  public async Task<IActionResult> Login([FromBody] LoginData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.Login
    {
      Data = request
    }, cancellationToken);

    if (result.Status == IdentityActionStatus.InvalidRequest)
      return BadRequest(result.Message);

    if (result.Status == IdentityActionStatus.Unauthorized)
      return Unauthorized();

    return NoContent();
  }

  [HttpPost("~/identity/logout")]
  public async Task<IActionResult> Logout(CancellationToken cancellationToken)
  {
    await mediator.Send(new Core.Command.identity.Logout(), cancellationToken);
    return NoContent();
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/identity/clients")]
  public async Task<IActionResult> CreateClient([FromBody] CreateClientData request, CancellationToken cancellationToken)
  {
    request ??= new CreateClientData
    {
      AllowAuthorizationCode = true,
      AllowClientCredentials = true,
      AllowRefreshToken = true
    };

    var result = await mediator.Send(new Core.Command.identity.CreateClient
    {
      Data = request
    }, cancellationToken);

    if (result.Status == IdentityActionStatus.InvalidRequest)
      return BadRequest(result.Message);

    if (result.Status == IdentityActionStatus.Conflict)
      return Conflict(result.Message);

    return Ok(new CreateClientResponse(result.ClientId, result.ClientSecret));
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/users")]
  [HttpGet("~/identity/users")]
  public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.identity.ListUsers(), cancellationToken);
    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/users")]
  public async Task<IActionResult> CreateUser([FromBody] CreateUserData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.CreateUser
    {
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPut("~/users/{id}")]
  public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.UpdateUser
    {
      Id = id,
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpDelete("~/users/{id}")]
  public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.DeleteUser
    {
      Id = id
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/roles")]
  [HttpGet("~/identity/roles")]
  public async Task<IActionResult> ListRoles(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.identity.ListRoles(), cancellationToken);
    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/roles")]
  public async Task<IActionResult> CreateRole([FromBody] CreateRoleData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.CreateRole
    {
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPut("~/roles/{id}")]
  public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.UpdateRole
    {
      Id = id,
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpDelete("~/roles/{id}")]
  public async Task<IActionResult> DeleteRole(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.identity.DeleteRole
    {
      Id = id
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/identity/apps")]
  public async Task<IActionResult> ListApps(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.identity.ListApps(), cancellationToken);
    return Ok(result);
  }

  public sealed record CreateClientResponse(string ClientId, string ClientSecret);

  private IActionResult MapIdentityCommandResult(IdentityCommandResult result)
  {
    return result.Status switch
    {
      IdentityActionStatus.Success => NoContent(),
      IdentityActionStatus.InvalidRequest => BadRequest(result.Message),
      IdentityActionStatus.NotFound => NotFound(result.Message),
      IdentityActionStatus.Conflict => Conflict(result.Message),
      IdentityActionStatus.Unauthorized => Unauthorized(),
      _ => BadRequest(result.Message)
    };
  }
}
