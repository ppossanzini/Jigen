using System.Composition;
using System.Runtime.InteropServices.JavaScript;
using Jigen.SemanticTools;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedTools;

namespace Jigen.TextEmbedding.Handlers;

[Export(typeof(IModule))]
public class Module: IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    var settings = configuration.GetSection("JigenEmbeddings").Get<EmbeddingSettings>();
    services.Configure<EmbeddingSettings>(configuration.GetSection("JigenEmbeddings"));
    
    services.AddSingleton<IEmbeddingGenerator>(_ => new QueuedEmbeddingGenerator(
      new OnnxEmbeddingGenerator(
        settings.TokenizerPath,
        settings.EmbeddingsModelPath,
        _.GetService<ILogger<OnnxEmbeddingGenerator>>(),
        settings.GeneratorOptions),
      settings.EmbeddingsMaxConcurrency,
      settings.EmbeddingsQueueCapacity,
      TimeSpan.FromSeconds(settings.EmbeddingsQueueTimeoutSeconds)));
    
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