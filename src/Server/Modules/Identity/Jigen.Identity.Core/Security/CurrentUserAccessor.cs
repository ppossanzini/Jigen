using System.Security.Claims;

namespace Jigen.Identity.Core.Security;

public interface ICurrentUserAccessor
{
  ClaimsPrincipal User { get; }
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
  public ClaimsPrincipal User { get; private set; } = new ClaimsPrincipal(new ClaimsIdentity());

  public void SetUser(ClaimsPrincipal user)
  {
    User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
  }
}
