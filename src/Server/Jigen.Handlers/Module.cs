using System.Composition;
using Jigen.Handlers.Model;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedTools;

namespace Jigen.Handlers;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    var settings = configuration.GetSection("JigenServer").Get<JigenServerSettings>();
    services.Configure<JigenServerSettings>(configuration.GetSection("JigenServer"));

    AppContext.SetData("GCHeapHardLimit", settings.MemoryLimitMB * 1024 * 1024);

    services.AddSingleton<SystemDB>(serviceProvider => new SystemDB(
      new StoreOptions()
      {
        DataBasePath = settings.DataFolderPath,
        DataBaseName = "system"
      }));

    services.AddSingleton<DatabasesManager>();
    services.AddSingleton<IDocumentSerializer>(serviceProvider => MessagePackDocumentSerializer.Instance);
    services.AddScoped<CQRS.DatabaseOwnershipGuard>();

    // Graceful shutdown: closes every store (releasing the exclusive lock
    // files) so the next start is not treated as crash recovery.
    services.AddHostedService<Model.StoreLifecycleService>();


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
}