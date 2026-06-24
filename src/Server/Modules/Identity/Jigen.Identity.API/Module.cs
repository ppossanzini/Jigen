using System.Composition;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Server;
using SharedTools;

namespace Jigen.Identity;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    
    services.AddAuthentication(options =>
    {
      options.DefaultScheme = IdentityConstants.ApplicationScheme;
      options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
      options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    });

    services.AddAuthorization(options =>
    {
      AddPermissionPolicy(options, AuthConstants.Claims.DatabaseCreate, AuthConstants.Roles.DatabaseAdmin);
      AddPermissionPolicy(options, AuthConstants.Claims.DatabaseDelete, AuthConstants.Roles.DatabaseAdmin);
      AddPermissionPolicy(options, AuthConstants.Claims.DatabaseAdmin, AuthConstants.Roles.DatabaseAdmin);

      AddPermissionPolicy(options, AuthConstants.Claims.CollectionCreate, AuthConstants.Roles.DatabaseAdmin);
      AddPermissionPolicy(options, AuthConstants.Claims.CollectionRead, AuthConstants.Roles.DatabaseAdmin);
      AddPermissionPolicy(options, AuthConstants.Claims.CollectionUpdate, AuthConstants.Roles.DatabaseAdmin);

      AddPermissionPolicy(options, AuthConstants.Claims.UserCreate, AuthConstants.Roles.SecurityAdmin);
      AddPermissionPolicy(options, AuthConstants.Claims.UserUpdate, AuthConstants.Roles.SecurityAdmin);
    });

    services.AddCurrentUserAccessor();

    services.AddOpenIddict()
      .AddServer(options =>
      {
        var issuer = configuration["JigenIdentity:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
          options.SetIssuer(new Uri(issuer));

        var opt = options
          .SetAuthorizationEndpointUris("/api/connect/authorize")
          .SetTokenEndpointUris("/api/connect/token")
          .SetIntrospectionEndpointUris("/api/connect/introspect")
          .SetRevocationEndpointUris("/api/connect/revocation")
          .AllowAuthorizationCodeFlow()
          .AllowImplicitFlow()
          .AllowPasswordFlow()
          .AllowHybridFlow()
          .AllowClientCredentialsFlow()
          .AllowRefreshTokenFlow()
          .DisableAccessTokenEncryption()
          .RequireProofKeyForCodeExchange()
          .AddDevelopmentEncryptionCertificate()
          .AddDevelopmentSigningCertificate()
          .UseAspNetCore()
          .EnableAuthorizationEndpointPassthrough()
          .EnableTokenEndpointPassthrough()
          .EnableStatusCodePagesIntegration();

        string[] modes = ["Random", "FromFile"];
        if (!modes.Contains( configuration["JigenServer:Https:Mode"]))
          opt.DisableTransportSecurityRequirement(); 
      });

    

    services.AddControllers()
      .AddApplicationPart(typeof(Module).Assembly);
  }

  public void OnStartup(IServiceProvider services)
  {
  }

  public void UseEndpoints(IEndpointRouteBuilder endpoints)
  {
  }

  public void PostStartup(IServiceProvider services)
  {
  }

  private static void AddPermissionPolicy(AuthorizationOptions options, string permission, string adminRole)
  {
    options.AddPolicy(permission, policy =>
    {
      policy.RequireAssertion(context =>
        context.User.IsInRole(adminRole) ||
        context.User.HasClaim(AuthConstants.ClaimTypes.Permission, permission));
    });
  }
}
