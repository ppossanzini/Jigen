using Hikyaku;
using Jigen.SemanticTools;
using Jigen.Embedding.Core.Commands;
using Microsoft.Extensions.Options;

namespace Jigen.Embedding.Handlers;

public class CommandHandlers(IEmbeddingGenerator generator, IImageEmbeddingGenerator imageGenerator, IOptions<EmbeddingSettings> settings)
  : IRequestHandler<Embedding.Core.Commands.CalculateEmbeddings, float[]>,
    IRequestHandler<Embedding.Core.Commands.CalculateEmbeddingsBatch, float[][]>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageEmbedding, float[]>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageEmbeddingBatch, float[][]>,
    IRequestHandler<Embedding.Core.Commands.CalculateImageTileEmbeddings, float[][]>
{
  public Task<float[]> Handle(CalculateEmbeddings request, CancellationToken cancellationToken)
  {
    return generator.GenerateEmbeddingAsync(request.Task ?? settings.Value.DefaultTask, request.Sentence, cancellationToken);
  }

  public Task<float[][]> Handle(CalculateEmbeddingsBatch request, CancellationToken cancellationToken)
  {
    var task = request.Task ?? settings.Value.DefaultTask;

    // Same task-prefix convention as GenerateEmbedding(task, input).
    var inputs = string.IsNullOrWhiteSpace(task)
      ? request.Sentences
      : Array.ConvertAll(request.Sentences, sentence => $"{task}: {sentence}");

    return generator.GenerateEmbeddingsAsync(inputs, cancellationToken);
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