using System.Composition;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Jigen.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Jigen.Identity.Security;
using OpenIddict.Server;
using SharedTools;

namespace Jigen.Identity;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    var dbPath = configuration["JigenIdentity:DbPath"] ?? "App_Data/identity.db";
    var fullPath = Path.IsPathRooted(dbPath)
      ? dbPath
      : Path.Combine(hostingEnvironment.ContentRootPath, dbPath);

    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
      Directory.CreateDirectory(directory);

    var connectionString = $"Data Source={fullPath}";

    services.AddDbContext<JigenIdentityDbContext>(options =>
    {
      options.UseSqlite(connectionString);
      options.UseOpenIddict();
    });

    services.AddIdentity<IdentityUser, IdentityRole>()
      .AddEntityFrameworkStores<JigenIdentityDbContext>()
      .AddDefaultTokenProviders();

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
      .AddCore(options =>
      {
        options.UseEntityFrameworkCore()
          .UseDbContext<JigenIdentityDbContext>();
      })
      .AddServer(options =>
      {
        var issuer = configuration["JigenIdentity:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
          options.SetIssuer(new Uri(issuer));

        options
          .SetAuthorizationEndpointUris("/connect/authorize")
          .SetTokenEndpointUris("/connect/token")
          .SetIntrospectionEndpointUris("/connect/introspect")
          .SetRevocationEndpointUris("/connect/revocation")
          .AllowAuthorizationCodeFlow()
          .AllowClientCredentialsFlow()
          .AllowRefreshTokenFlow()
          .RequireProofKeyForCodeExchange()
          .AddDevelopmentEncryptionCertificate()
          .AddDevelopmentSigningCertificate()
          .UseAspNetCore()
          .EnableAuthorizationEndpointPassthrough()
          .EnableTokenEndpointPassthrough()
          .EnableStatusCodePagesIntegration();
      });

    services.AddHostedService<IdentitySeeder>();

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
