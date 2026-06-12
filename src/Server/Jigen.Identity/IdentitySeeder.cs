using System.Security.Claims;
using Jigen.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace Jigen.Identity;

public sealed class IdentitySeeder : IHostedService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IConfiguration _configuration;

  public IdentitySeeder(IServiceProvider serviceProvider, IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _configuration = configuration;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    using var scope = _serviceProvider.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<JigenIdentityDbContext>();
    await dbContext.Database.EnsureCreatedAsync(cancellationToken);

    await SeedUserAsync(scope.ServiceProvider, cancellationToken);
    await SeedRolesAsync(scope.ServiceProvider, cancellationToken);
    await SeedClientAsync(scope.ServiceProvider, cancellationToken);
    await SeedScopeAsync(scope.ServiceProvider, cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  private async Task SeedUserAsync(IServiceProvider services, CancellationToken cancellationToken)
  {
    var userName = _configuration["JigenIdentity:SeedUser:UserName"];
    var password = _configuration["JigenIdentity:SeedUser:Password"];

    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
      return;

    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var existing = await userManager.FindByNameAsync(userName);
    if (existing != null)
    {
      await AssignSeedUserRolesAndPermissionsAsync(userManager, existing, cancellationToken);
      return;
    }

    var user = new IdentityUser { UserName = userName };
    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
      throw new InvalidOperationException($"Failed to create seed user '{userName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");

    await AssignSeedUserRolesAndPermissionsAsync(userManager, user, cancellationToken);
  }

  private async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken)
  {
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await EnsureRoleAsync(roleManager, AuthConstants.Roles.DatabaseAdmin, cancellationToken);
    await EnsureRoleAsync(roleManager, AuthConstants.Roles.SecurityAdmin, cancellationToken);
  }

  private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName, CancellationToken cancellationToken)
  {
    if (await roleManager.RoleExistsAsync(roleName))
      return;

    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
    if (!result.Succeeded)
      throw new InvalidOperationException($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
  }

  private async Task AssignSeedUserRolesAndPermissionsAsync(UserManager<IdentityUser> userManager, IdentityUser user, CancellationToken cancellationToken)
  {
    var roles = _configuration.GetSection("JigenIdentity:SeedUser:Roles").Get<string[]>() ?? [];
    if (roles.Length > 0)
    {
      var addRolesResult = await userManager.AddToRolesAsync(user, roles);
      if (!addRolesResult.Succeeded)
        throw new InvalidOperationException($"Failed to assign roles to seed user '{user.UserName}': {string.Join(", ", addRolesResult.Errors.Select(e => e.Description))}");
    }

    var permissions = _configuration.GetSection("JigenIdentity:SeedUser:Permissions").Get<string[]>() ?? [];
    if (permissions.Length > 0)
    {
      var existingClaims = await userManager.GetClaimsAsync(user);
      var toAdd = permissions
        .Where(permission => existingClaims.All(c => c.Type != AuthConstants.ClaimTypes.Permission || c.Value != permission))
        .Select(permission => new Claim(AuthConstants.ClaimTypes.Permission, permission))
        .ToList();

      if (toAdd.Count > 0)
      {
        var addClaimsResult = await userManager.AddClaimsAsync(user, toAdd);
        if (!addClaimsResult.Succeeded)
          throw new InvalidOperationException($"Failed to assign permissions to seed user '{user.UserName}': {string.Join(", ", addClaimsResult.Errors.Select(e => e.Description))}");
      }
    }
  }

  private async Task SeedClientAsync(IServiceProvider services, CancellationToken cancellationToken)
  {
    var clientId = _configuration["JigenIdentity:DefaultClient:ClientId"];
    if (string.IsNullOrWhiteSpace(clientId))
      return;

    var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
    if (await manager.FindByClientIdAsync(clientId, cancellationToken) != null)
      return;

    var redirectUris = _configuration.GetSection("JigenIdentity:DefaultClient:RedirectUris").Get<string[]>() ?? [];
    var postLogoutRedirectUris = _configuration.GetSection("JigenIdentity:DefaultClient:PostLogoutRedirectUris").Get<string[]>() ?? [];

    var descriptor = new OpenIddictApplicationDescriptor
    {
      ClientId = clientId,
      ClientSecret = _configuration["JigenIdentity:DefaultClient:ClientSecret"],
      DisplayName = _configuration["JigenIdentity:DefaultClient:DisplayName"] ?? clientId,
      Permissions =
      {
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token,
        "endpoints:userinfo",
        OpenIddictConstants.Permissions.Endpoints.Introspection,
        OpenIddictConstants.Permissions.Endpoints.Revocation,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Scopes.Address,
        OpenIddictConstants.Permissions.Scopes.Email,
        OpenIddictConstants.Permissions.Scopes.Profile,
        OpenIddictConstants.Permissions.Scopes.Roles,
        OpenIddictConstants.Permissions.Prefixes.Scope + "jigen_api",
        OpenIddictConstants.Permissions.Prefixes.Scope + "openid"
      }
    };

    foreach (var uri in redirectUris)
      if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        descriptor.RedirectUris.Add(parsed);

    foreach (var uri in postLogoutRedirectUris)
      if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        descriptor.PostLogoutRedirectUris.Add(parsed);

    await manager.CreateAsync(descriptor, cancellationToken);
  }

  private async Task SeedScopeAsync(IServiceProvider services, CancellationToken cancellationToken)
  {
    var manager = services.GetRequiredService<IOpenIddictScopeManager>();
    const string scopeName = "jigen_api";

    if (await manager.FindByNameAsync(scopeName, cancellationToken) != null)
      return;

    var descriptor = new OpenIddictScopeDescriptor
    {
      Name = scopeName,
      DisplayName = "Jigen API",
      Resources = { scopeName }
    };

    await manager.CreateAsync(descriptor, cancellationToken);
  }
}
