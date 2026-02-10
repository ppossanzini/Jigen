using System.Composition;
using Jigen.Handlers.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedTools;

namespace Jigen.Handlers;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    var settings = configuration.GetSection("JigenServer").Get<JigenServerSettings>();
    services.Configure<JigenServerSettings>(configuration.GetSection("JigenServer"));

    services.AddSingleton<SystemDB>(new SystemDB(
      new StoreOptions()
      {
        DataBasePath = settings.DataFolderPath,
        DataBaseName = "system"
      }
    ));
    
    services.AddSingleton<DatabasesManager>();
  }

  public void OnStartup(IServiceProvider services)
  {
  }

  public void PostStartup(IServiceProvider services)
  {
  }
}