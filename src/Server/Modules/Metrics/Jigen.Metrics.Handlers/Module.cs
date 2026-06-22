using System.Composition;
using Jigen.Metrics.Handlers.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedTools;

namespace Jigen.Metrics.Handlers;

[Export(typeof(IModule))]
public class Module : IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    services.AddSingleton<ServerStatusHistoryService>();
    services.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<ServerStatusHistoryService>());
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