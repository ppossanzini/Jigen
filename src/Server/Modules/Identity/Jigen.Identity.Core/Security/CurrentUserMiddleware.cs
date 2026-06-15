using Microsoft.AspNetCore.Http;

namespace Jigen.Identity.Core.Security;

public sealed class CurrentUserMiddleware
{
  private readonly RequestDelegate _next;

  public CurrentUserMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public async Task InvokeAsync(HttpContext context, CurrentUserAccessor accessor)
  {
    accessor.SetUser(context.User);
    await _next(context);
  }
}
