using System.Composition;
using System.Runtime.InteropServices.JavaScript;
using Jigen.SemanticTools;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedTools;

namespace Jigen.Embedding.Handlers;

[Export(typeof(IModule))]
public class Module: IModule
{
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    var settings = configuration.GetSection("JigenEmbeddings").Get<EmbeddingSettings>();
    services.Configure<EmbeddingSettings>(configuration.GetSection("JigenEmbeddings"));

    services.AddSingleton<IEmbeddingGeneratorRegistry>(provider =>
    {
      var configuredModels = settings.Models is { Count: > 0 }
        ? settings.Models
        : new Dictionary<string, TextEmbeddingModelSettings>(StringComparer.OrdinalIgnoreCase)
        {
          ["default"] = new()
          {
            TokenizerPath = settings.TokenizerPath,
            ModelPath = settings.EmbeddingsModelPath,
            GeneratorOptions = settings.GeneratorOptions ?? new EmbeddingGeneratorOptions(),
            MaxConcurrency = settings.EmbeddingsMaxConcurrency,
            QueueCapacity = settings.EmbeddingsQueueCapacity,
            QueueTimeoutSeconds = settings.EmbeddingsQueueTimeoutSeconds,
            DefaultTask = settings.DefaultTask
          }
        };

      var generators = new Dictionary<string, IEmbeddingGenerator>(StringComparer.OrdinalIgnoreCase);
      var defaultTasks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var profiles = new Dictionary<string, EmbeddingModelProfile>(StringComparer.OrdinalIgnoreCase);
      foreach (var (name, model) in configuredModels)
      {
        if (string.IsNullOrWhiteSpace(name))
          throw new InvalidOperationException("Embedding model names cannot be empty.");

        var options = model.GeneratorOptions ?? new EmbeddingGeneratorOptions();
        if (options.IntraOpNumThreads <= 0)
          options.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / Math.Max(model.MaxConcurrency, 1));

        generators.Add(name, new QueuedEmbeddingGenerator(
          new OnnxEmbeddingGenerator(
            model.TokenizerPath,
            model.ModelPath,
            provider.GetService<ILogger<OnnxEmbeddingGenerator>>(),
            options),
          model.MaxConcurrency,
          model.QueueCapacity,
          TimeSpan.FromSeconds(model.QueueTimeoutSeconds),
          options.MaxBatchSize));
        defaultTasks[name] = model.DefaultTask;
        profiles[name] = options.Profile;
      }

      var defaultModel = settings.Models is { Count: > 0 } ? settings.DefaultModel : "default";
      if (!generators.ContainsKey(defaultModel))
        throw new InvalidOperationException($"Default embedding model '{defaultModel}' is not configured.");

      return new EmbeddingGeneratorRegistry(defaultModel, generators, defaultTasks, profiles);
    });
    services.AddSingleton<IEmbeddingGenerator, DefaultEmbeddingGenerator>();

    // Image embeddings are opt-in: without ImagesModelPath the server keeps
    // working (text only) and image requests fail with a clear error.
    services.AddSingleton<IImageEmbeddingGenerator>(_ =>
    {
      if (string.IsNullOrWhiteSpace(settings.ImagesModelPath))
        return new UnconfiguredImageEmbeddingGenerator();

      var imageOptions = settings.ImageGeneratorOptions ?? new ImageEmbeddingGeneratorOptions();
      if (imageOptions.IntraOpNumThreads <= 0)
        imageOptions.IntraOpNumThreads =
          Math.Max(1, Environment.ProcessorCount / Math.Max(settings.ImageEmbeddingsMaxConcurrency, 1));

      return new QueuedImageEmbeddingGenerator(
        new OnnxImageEmbeddingGenerator(
          settings.ImagesModelPath,
          _.GetService<ILogger<OnnxImageEmbeddingGenerator>>(),
          imageOptions),
        settings.ImageEmbeddingsMaxConcurrency,
        settings.ImageEmbeddingsQueueCapacity,
        TimeSpan.FromSeconds(settings.ImageEmbeddingsQueueTimeoutSeconds),
        imageOptions.MaxBatchSize);
    });
    
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
