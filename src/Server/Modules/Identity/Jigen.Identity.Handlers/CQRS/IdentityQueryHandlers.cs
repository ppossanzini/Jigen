using Hikyaku;
using Jigen.Identity.Core.Dto;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Jigen.Identity.CQRS;

public class IdentityQueryHandlers(
  UserManager<IdentityUser> userManager,
  RoleManager<IdentityRole> roleManager,
  IOpenIddictApplicationManager applicationManager) :
  IRequestHandler<Core.Query.ListUsers, IEnumerable<UserSummary>>,
  IRequestHandler<Core.Query.ListRoles, IEnumerable<RoleSummary>>,
  IRequestHandler<Core.Query.ListApps, IEnumerable<AppSummary>>,
  IRequestHandler<Core.Query.GetUserDetail, UserDetail?>,
  IRequestHandler<Core.Query.GetUsersInRole, IEnumerable<UserSummary>?>
{
  public async Task<IEnumerable<UserSummary>> Handle(Core.Query.ListUsers request, CancellationToken cancellationToken)
  {
    // Only return minimal, non-sensitive user fields.
    var users = await userManager.Users
      .AsNoTracking()
      .Select(u => new UserSummary
      {
        Id = u.Id,
        UserName = u.UserName
      })
      .ToListAsync(cancellationToken);

    return users;
  }

  public async Task<IEnumerable<RoleSummary>> Handle(Core.Query.ListRoles request, CancellationToken cancellationToken)
  {
    var roles = await roleManager.Roles
      .AsNoTracking()
      .Select(r => new RoleSummary
      {
        Id = r.Id,
        Name = r.Name
      })
      .ToListAsync(cancellationToken);

    return roles;
  }

  public async Task<IEnumerable<AppSummary>> Handle(Core.Query.ListApps request, CancellationToken cancellationToken)
  {
    var apps = new List<AppSummary>();

    await foreach (var application in applicationManager.ListAsync(cancellationToken: cancellationToken))
    {
      var clientId = await applicationManager.GetClientIdAsync(application, cancellationToken);
      var displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);

      apps.Add(new AppSummary
      {
        ClientId = clientId,
        DisplayName = displayName
      });
    }

    return apps;
  }

  public async Task<UserDetail?> Handle(Core.Query.GetUserDetail request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.Id))
      return null;

    var user = await userManager.FindByIdAsync(request.Id);
    if (user == null)
      return null;

    var roles = await userManager.GetRolesAsync(user);
    var claims = await userManager.GetClaimsAsync(user);
    var permissions = claims
      .Where(c => c.Type == AuthConstants.ClaimTypes.Permission)
      .Select(c => c.Value)
      .Distinct()
      .ToArray();

    return new UserDetail
    {
      Id = user.Id,
      UserName = user.UserName,
      Roles = roles.ToArray(),
      Permissions = permissions
    };
  }

  public async Task<IEnumerable<UserSummary>?> Handle(Core.Query.GetUsersInRole request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.RoleId))
      return null;

    var role = await roleManager.FindByIdAsync(request.RoleId);
    if (role == null || string.IsNullOrWhiteSpace(role.Name))
      return null;

    var users = await userManager.GetUsersInRoleAsync(role.Name);
    return users.Select(u => new UserSummary
    {
      Id = u.Id,
      UserName = u.UserName
    }).ToList();
  }
}
