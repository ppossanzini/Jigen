using System.Composition;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Jigen.Identity.Handlers;
using Microsoft.AspNetCore.Authorization;

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
    


    services.AddOpenIddict()
      .AddCore(options =>
      {
        options.UseEntityFrameworkCore()
          .UseDbContext<JigenIdentityDbContext>();
      });

    services.AddHostedService<IdentitySeeder>();
    
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

  }
}
