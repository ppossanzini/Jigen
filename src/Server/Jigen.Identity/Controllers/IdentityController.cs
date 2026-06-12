using System.Security.Cryptography;
using Jigen.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Jigen.Identity.Controllers;

[ApiController]
public class IdentityController : ControllerBase
{
  private readonly SignInManager<IdentityUser> _signInManager;
  private readonly IOpenIddictApplicationManager _applicationManager;

  public IdentityController(
    SignInManager<IdentityUser> signInManager,
    IOpenIddictApplicationManager applicationManager)
  {
    _signInManager = signInManager;
    _applicationManager = applicationManager;
  }

  [HttpPost("~/identity/login")]
  public async Task<IActionResult> Login([FromBody] LoginRequest request)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
      return BadRequest("Username and password are required.");

    var result = await _signInManager.PasswordSignInAsync(request.UserName, request.Password, false, false);
    if (!result.Succeeded)
      return Unauthorized();

    return NoContent();
  }

  [HttpPost("~/identity/logout")]
  public async Task<IActionResult> Logout()
  {
    await _signInManager.SignOutAsync();
    return NoContent();
  }

  [Authorize(Roles = AuthConstants.Roles.SecurityAdmin)]
  [HttpPost("~/identity/clients")]
  public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest? request)
  {
    request ??= new CreateClientRequest();

    var clientId = request.ClientId ?? GenerateSecret(24);
    var clientSecret = GenerateSecret(48);

    if (await _applicationManager.FindByClientIdAsync(clientId) != null)
      return Conflict("ClientId already exists.");

    if (request.AllowAuthorizationCode && (request.RedirectUris == null || request.RedirectUris.Length == 0))
      return BadRequest("RedirectUris are required when Authorization Code flow is enabled.");

    var descriptor = new OpenIddictApplicationDescriptor
    {
      ClientId = clientId,
      ClientSecret = clientSecret,
      DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? clientId : request.DisplayName
    };

    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
    descriptor.Permissions.Add("endpoints:userinfo");

    if (request.AllowAuthorizationCode)
    {
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
    }

    if (request.AllowClientCredentials)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

    if (request.AllowRefreshToken)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);

    var scopes = request.Scopes?.Length > 0
      ? request.Scopes
      : new[] { "jigen_api", "openid" };

    foreach (var scope in scopes)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);

    if (request.RedirectUris != null)
    {
      foreach (var uri in request.RedirectUris)
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
          descriptor.RedirectUris.Add(parsed);
    }

    if (request.PostLogoutRedirectUris != null)
    {
      foreach (var uri in request.PostLogoutRedirectUris)
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
          descriptor.PostLogoutRedirectUris.Add(parsed);
    }

    await _applicationManager.CreateAsync(descriptor);

    return Ok(new CreateClientResponse(clientId, clientSecret));
  }

  private static string GenerateSecret(int byteLength)
  {
    var bytes = RandomNumberGenerator.GetBytes(byteLength);
    return Base64UrlEncode(bytes);
  }

  private static string Base64UrlEncode(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
  }

  public sealed record LoginRequest(string UserName, string Password);

  public sealed class CreateClientRequest
  {
    public string? ClientId { get; init; }
    public string? DisplayName { get; init; }
    public bool AllowAuthorizationCode { get; init; } = true;
    public bool AllowClientCredentials { get; init; } = true;
    public bool AllowRefreshToken { get; init; } = true;
    public string[]? RedirectUris { get; init; }
    public string[]? PostLogoutRedirectUris { get; init; }
    public string[]? Scopes { get; init; }
  }

  public sealed record CreateClientResponse(string ClientId, string ClientSecret);
}
