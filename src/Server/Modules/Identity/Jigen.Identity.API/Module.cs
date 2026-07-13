using System.Composition;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Validation.ServerIntegration;
using SharedTools;

namespace Jigen.Identity;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    
    // Business/API requests authenticate with an OpenIddict bearer token; the
    // ASP.NET Identity cookie is only used by the browser to complete the
    // initial /connect/authorize step. Route each request to the right
    // handler based on whether it carries an Authorization: Bearer header.
    services.AddAuthentication(options =>
    {
      options.DefaultScheme = AuthConstants.Schemes.CookieOrBearer;
      options.DefaultAuthenticateScheme = AuthConstants.Schemes.CookieOrBearer;
      options.DefaultChallengeScheme = AuthConstants.Schemes.CookieOrBearer;
    })
    .AddPolicyScheme(AuthConstants.Schemes.CookieOrBearer, "Identity cookie or OpenIddict bearer token", policyOptions =>
    {
      policyOptions.ForwardDefaultSelector = context =>
      {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
          ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
          : IdentityConstants.ApplicationScheme;
      };
    });

    // AddIdentity() (Jigen.Identity.Handlers module) also calls AddAuthentication(),
    // which would otherwise silently reset the default scheme back to the Identity
    // cookie depending on module load order. PostConfigure always runs after every
    // Configure<AuthenticationOptions> delegate, so this wins regardless of order.
    services.PostConfigure<AuthenticationOptions>(options =>
    {
      options.DefaultScheme = AuthConstants.Schemes.CookieOrBearer;
      options.DefaultAuthenticateScheme = AuthConstants.Schemes.CookieOrBearer;
      options.DefaultChallengeScheme = AuthConstants.Schemes.CookieOrBearer;
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
      })
      .AddValidation(options =>
      {
        // Server and Validation run in the same process, so tokens are
        // checked via direct in-process calls instead of network introspection.
        options.UseLocalServer();
        options.UseAspNetCore();
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
