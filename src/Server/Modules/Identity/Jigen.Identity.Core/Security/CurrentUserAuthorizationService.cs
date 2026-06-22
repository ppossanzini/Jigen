using Microsoft.AspNetCore.Authorization;

namespace Jigen.Identity.Core.Security;

public interface ICurrentUserAuthorizationService
{
  Task<bool> AuthorizeAsync(string permission, CancellationToken cancellationToken = default);
  Task EnsureAsync(string permission, CancellationToken cancellationToken = default);
}

public sealed class CurrentUserAuthorizationService : ICurrentUserAuthorizationService
{
  private readonly IAuthorizationService _authorizationService;
  private readonly ICurrentUserAccessor _currentUserAccessor;

  public CurrentUserAuthorizationService(
    IAuthorizationService authorizationService,
    ICurrentUserAccessor currentUserAccessor)
  {
    _authorizationService = authorizationService;
    _currentUserAccessor = currentUserAccessor;
  }

  public async Task<bool> AuthorizeAsync(string permission, CancellationToken cancellationToken = default)
  {
    var result = await _authorizationService.AuthorizeAsync(
      _currentUserAccessor.User,
      permission);

    return result.Succeeded;
  }

  public async Task EnsureAsync(string permission, CancellationToken cancellationToken = default)
  {
    var ok = await AuthorizeAsync(permission, cancellationToken);
    if (!ok)
      throw new UnauthorizedAccessException(permission);
  }
}
