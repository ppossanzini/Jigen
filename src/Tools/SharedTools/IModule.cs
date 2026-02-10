using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedTools
{
  public interface IModule
  {
    void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment);
    void OnStartup(IServiceProvider services);
    void PostStartup(IServiceProvider services);
  }
}