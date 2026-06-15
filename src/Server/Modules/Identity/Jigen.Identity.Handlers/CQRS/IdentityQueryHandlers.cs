using Hikyaku;
using Jigen.Identity.Core.Dto;
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
  IRequestHandler<Core.Query.ListApps, IEnumerable<AppSummary>>
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
}