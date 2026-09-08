using Hikyaku;
using Jigen.SemanticTools;
using Jigen.Embedding.Core.Commands;

namespace Jigen.Embedding.Handlers;

public class CommandHandlers(IEmbeddingGeneratorRegistry registry, IImageEmbeddingGenerator imageGenerator)
  : IRequestHandler<Embedding.Core.Commands.CalculateEmbeddings, float[]>,
    IRequestHandler<Embedding.Core.Commands.CalculateEmbeddingsBatch, float[][]>,
    IRequestHandler<Embedding.Core.Commands.CalculateEmbeddingsMulti, Dictionary<string, float[]>>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageEmbedding, float[]>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageEmbeddingBatch, float[][]>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageTileEmbeddings, float[][]>
{
  public Task<float[]> Handle(CalculateEmbeddings request, CancellationToken cancellationToken)
  {
    var generator = registry.Get(request.Model);
    var task = string.IsNullOrWhiteSpace(request.Task) ? registry.GetDefaultTask(request.Model) : request.Task;
    return generator.GenerateEmbeddingAsync(registry.PrepareInput(request.Model, task, request.Sentence), cancellationToken);
  }

  public Task<float[][]> Handle(CalculateEmbeddingsBatch request, CancellationToken cancellationToken)
  {
    var generator = registry.Get(request.Model);
    var task = string.IsNullOrWhiteSpace(request.Task) ? registry.GetDefaultTask(request.Model) : request.Task;

    // Same task-prefix convention as GenerateEmbedding(task, input).
    var inputs = Array.ConvertAll(request.Sentences,
      sentence => registry.PrepareInput(request.Model, task, sentence));

    return generator.GenerateEmbeddingsAsync(inputs, cancellationToken);
  }

  public async Task<Dictionary<string, float[]>> Handle(CalculateEmbeddingsMulti request, CancellationToken cancellationToken)
  {
    var models = request.Models is { Length: > 0 }
      ? request.Models.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
      : [registry.DefaultModel];

    var pending = models.Select(async model =>
    {
      var generator = registry.Get(model);
      var task = string.IsNullOrWhiteSpace(request.Task) ? registry.GetDefaultTask(model) : request.Task;
      var input = registry.PrepareInput(model, task, request.Sentence);
      var vector = await generator.GenerateEmbeddingAsync(input, cancellationToken);
      return (model, vector);
    });

    var completed = await Task.WhenAll(pending);
    return completed.ToDictionary(item => item.model, item => item.vector, StringComparer.OrdinalIgnoreCase);
  }

  public Task<float[]> Handle(CalculateImageEmbedding request, CancellationToken cancellationToken)
  {
    return imageGenerator.GenerateImageEmbeddingAsync(request.ImageBytes, cancellationToken);
  }

  public Task<float[][]> Handle(CalculateImageEmbeddingBatch request, CancellationToken cancellationToken)
  {
    return imageGenerator.GenerateImageEmbeddingsAsync(request.Images, cancellationToken);
  }

  public Task<float[][]> Handle(CalculateImageTileEmbeddings request, CancellationToken cancellationToken)
  {
    return imageGenerator.GenerateImageTileEmbeddingsAsync(request.ImageBytes, cancellationToken);
  }
}
