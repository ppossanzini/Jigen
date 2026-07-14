using Hikyaku;
using Jigen.SemanticTools;
using Jigen.TextEmbedding.Core.Commands;
using Microsoft.Extensions.Options;

namespace Jigen.TextEmbedding.Handlers;

public class CommandHandlers(IEmbeddingGenerator generator, IOptions<EmbeddingSettings> settings)
  : IRequestHandler<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings, float[]>,
    IRequestHandler<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddingsBatch, float[][]>
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
}