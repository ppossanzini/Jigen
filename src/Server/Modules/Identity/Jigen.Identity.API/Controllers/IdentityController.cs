using Hikyaku;
using Jigen.Identity.Core.Command;
using Jigen.Identity.Core.Dto;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.Identity.Controllers;

[ApiController]
public class IdentityController(IHikyaku mediator) : ControllerBase
{
  [HttpPost("~/identity/login")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Login([FromBody] LoginData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.Login
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
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<IActionResult> Logout(CancellationToken cancellationToken)
  {
    await mediator.Send(new Core.Command.Logout(), cancellationToken);
    return NoContent();
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/identity/clients")]
  [ProducesResponseType(typeof(CreateClientResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> CreateClient([FromBody] CreateClientData request, CancellationToken cancellationToken)
  {
    request ??= new CreateClientData
    {
      AllowAuthorizationCode = true,
      AllowClientCredentials = true,
      AllowRefreshToken = true
    };

    var result = await mediator.Send(new CreateClient
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
  [ProducesResponseType(typeof(IEnumerable<UserSummary>), StatusCodes.Status200OK)]
  public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.ListUsers(), cancellationToken);
    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/users")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> CreateUser([FromBody] CreateUserData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.CreateUser
    {
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPut("~/users/{id}")]
  [ProducesResponseType(typeof(UserDetail), StatusCodes.Status200OK)]
  public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.UpdateUser
    {
      Id = id,
      Data = request
    }, cancellationToken);

    if (result.Status == IdentityActionStatus.Success)
    {
      var detail = await mediator.Send(new Core.Query.GetUserDetail
      {
        Id = id
      }, cancellationToken);

      if (detail == null)
        return NotFound("User not found.");

      return Ok(detail);
    }

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpDelete("~/users/{id}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.DeleteUser
    {
      Id = id
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/roles")]
  [HttpGet("~/identity/roles")]
  [ProducesResponseType(typeof(IEnumerable<RoleSummary>), StatusCodes.Status200OK)]
  public async Task<IActionResult> ListRoles(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.ListRoles(), cancellationToken);
    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/users/{id}")]
  [HttpGet("~/identity/users/{id}")]
  [ProducesResponseType(typeof(UserDetail), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetUserDetail(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.GetUserDetail
    {
      Id = id
    }, cancellationToken);

    if (result == null)
      return NotFound("User not found.");

    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/roles/{id}/users")]
  [HttpGet("~/identity/roles/{id}/users")]
  [ProducesResponseType(typeof(IEnumerable<UserSummary>), StatusCodes.Status200OK)]
  public async Task<IActionResult> ListUsersInRole(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.GetUsersInRole
    {
      RoleId = id
    }, cancellationToken);

    if (result == null)
      return NotFound("Role not found.");

    return Ok(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/roles")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> CreateRole([FromBody] CreateRoleData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.CreateRole
    {
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPut("~/roles/{id}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleData request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.UpdateRole
    {
      Id = id,
      Data = request
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpDelete("~/roles/{id}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> DeleteRole(string id, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Command.DeleteRole
    {
      Id = id
    }, cancellationToken);

    return MapIdentityCommandResult(result);
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpGet("~/identity/apps")]
  [ProducesResponseType(typeof(IEnumerable<AppSummary>), StatusCodes.Status200OK)]
  public async Task<IActionResult> ListApps(CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new Core.Query.ListApps(), cancellationToken);
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
