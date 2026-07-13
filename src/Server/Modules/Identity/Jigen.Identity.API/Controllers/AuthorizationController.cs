using System.Security.Claims;
using Hikyaku;
using Jigen.Identity.Core.Dto;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace Jigen.Identity.API.Controllers;

[ApiController]
public class AuthorizationController (
  IHikyaku mediator,
  UserManager<IdentityUser> userManager,
  SignInManager<IdentityUser> signInManager)
  : Controller
{
  [HttpGet("~/api/connect/authorize")]
  [HttpPost("~/api/connect/authorize")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Authorize()
  {
    var request = HttpContext.GetOpenIddictServerRequest()
                  ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

    if (User.Identity?.IsAuthenticated != true)
      return Unauthorized(new { error = "login_required" });

    var user = await userManager.GetUserAsync(User);
    if (user == null)
      return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    var principal = await signInManager.CreateUserPrincipalAsync(user);
    principal.SetClaim(OpenIddictConstants.Claims.Subject, await userManager.GetUserIdAsync(user));
    principal.SetClaim(OpenIddictConstants.Claims.PreferredUsername, await userManager.GetUserNameAsync(user));

    // CreateUserPrincipalAsync() adds roles as ClaimTypes.Role (the long XML URI), which
    // [Authorize(Roles=...)] matches fine for cookie auth. But OpenIddict's access token
    // validation sets RoleClaimType to the short "role" claim, so without this the same
    // roles silently fail to round-trip through a bearer token and every role check 403s.
    var roles = await userManager.GetRolesAsync(user);
    principal.SetClaims(OpenIddictConstants.Claims.Role, [..roles]);

    principal.SetScopes(request.GetScopes());
    principal.SetResources("jigen_api");

    foreach (var claim in principal.Claims)
      claim.SetDestinations(GetDestinations(claim));

    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
  }

  [HttpPost("~/api/connect/token")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
  {
    var request = HttpContext.GetOpenIddictServerRequest()
                  ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
    
    var result = await mediator.Send(new Core.Command.ExchangeToken
    {
      Data = new ExchangeTokenData
      {
        GrantType = request.GrantType,
        ClientId = request.ClientId,
        Scopes = request.GetScopes().ToArray()
      }
    }, cancellationToken);

    if (result.Status == IdentityActionStatus.InvalidRequest)
      throw new InvalidOperationException(result.Message ?? "The specified grant type is not supported.");

    if (result.UseAuthenticatedPrincipal)
    {
      var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
      if (authenticateResult.Principal == null)
        return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

      return SignIn(authenticateResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    identity.AddClaim(OpenIddictConstants.Claims.Subject, result.Subject, OpenIddictConstants.Destinations.AccessToken);
    identity.AddClaim(OpenIddictConstants.Claims.ClientId, result.ClientId, OpenIddictConstants.Destinations.AccessToken);

    var principal = new ClaimsPrincipal(identity);
    principal.SetScopes(result.Scopes ?? Array.Empty<string>());
    principal.SetResources("jigen_api");

    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
  }

  [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
  [HttpGet("~/api/connect/userinfo")]
  [HttpPost("~/api/connect/userinfo")]
  [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Userinfo()
  {
    var user = await userManager.GetUserAsync(User);
    if (user == null)
    {
      var subject = User.FindFirstValue(OpenIddictConstants.Claims.Subject);
      if (!string.IsNullOrWhiteSpace(subject))
        user = await userManager.FindByIdAsync(subject);
    }

    if (user == null)
      return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    return Ok(new Dictionary<string, object>
    {
      [OpenIddictConstants.Claims.Subject] = await userManager.GetUserIdAsync(user),
      [OpenIddictConstants.Claims.PreferredUsername] = await userManager.GetUserNameAsync(user)
    });
  }

  private static IEnumerable<string> GetDestinations(Claim claim)
  {
    if (claim.Type is OpenIddictConstants.Claims.Name or
        OpenIddictConstants.Claims.PreferredUsername or
        OpenIddictConstants.Claims.Subject)
    {
      yield return OpenIddictConstants.Destinations.AccessToken;
      yield return OpenIddictConstants.Destinations.IdentityToken;
      yield break;
    }

    yield return OpenIddictConstants.Destinations.AccessToken;
  }
}