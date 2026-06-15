using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jigen.Identity.Core.Security;

public static class CurrentUserExtensions
{
  public static IServiceCollection AddCurrentUserAccessor(this IServiceCollection services)
  {
    services.AddScoped<CurrentUserAccessor>();
    services.AddScoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUserAccessor>());
    services.AddScoped<ICurrentUserAuthorizationService, CurrentUserAuthorizationService>();
    return services;
  }

  public static IApplicationBuilder UseCurrentUserAccessor(this IApplicationBuilder app)
  {
    return app.UseMiddleware<CurrentUserMiddleware>();
  }
}
