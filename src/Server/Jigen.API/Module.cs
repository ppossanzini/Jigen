using System.Composition;
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
  }

  public void OnStartup(IServiceProvider services)
  {
  }

  public void PostStartup(IServiceProvider services)
  {
  }
}