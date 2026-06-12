using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Jigen.Identity.Controllers;

[ApiController]
public class AuthorizationController (
  UserManager<IdentityUser> userManager,
  SignInManager<IdentityUser> signInManager)
  : Controller
{
  [HttpGet("~/connect/authorize")]
  [HttpPost("~/connect/authorize")]
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
    principal.SetScopes(request.GetScopes());
    principal.SetResources("jigen_api");

    foreach (var claim in principal.Claims)
      claim.SetDestinations(GetDestinations(claim));

    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
  }

  [HttpPost("~/connect/token")]
  public async Task<IActionResult> Exchange()
  {
    var request = HttpContext.GetOpenIddictServerRequest()
                  ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

    if (request.IsClientCredentialsGrantType())
    {
      var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
      identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!, OpenIddictConstants.Destinations.AccessToken);
      identity.AddClaim(OpenIddictConstants.Claims.ClientId, request.ClientId!, OpenIddictConstants.Destinations.AccessToken);

      var principal = new ClaimsPrincipal(identity);
      principal.SetScopes(request.GetScopes());
      principal.SetResources("jigen_api");

      return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
    {
      var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
      if (result.Principal == null)
        return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

      return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    throw new InvalidOperationException("The specified grant type is not supported.");
  }

  [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
  [HttpGet("~/connect/userinfo")]
  [HttpPost("~/connect/userinfo")]
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