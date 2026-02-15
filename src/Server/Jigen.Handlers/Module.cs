using System.Composition;
using Jigen.Handlers.Model;
using Jigen.SemanticTools;
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

    services.AddSingleton<SystemDB>(serviceProvider => new SystemDB(
      new StoreOptions()
      {
        DataBasePath = settings.DataFolderPath,
        DataBaseName = "system"
      }));

    services.AddSingleton<DatabasesManager>();
    services.AddTransient<IEmbeddingGenerator>(p => new OnnxEmbeddingGenerator(settings.TokenizerPath, settings.EmbeddingsModelPath));
  }

  public void OnStartup(IServiceProvider services)
  {
  }

  public void PostStartup(IServiceProvider services)
  {
  }
}