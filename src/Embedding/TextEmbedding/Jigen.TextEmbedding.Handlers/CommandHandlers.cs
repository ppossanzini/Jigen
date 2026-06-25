using Hikyaku;
using Jigen.SemanticTools;
using Jigen.TextEmbedding.Core.Commands;
using Microsoft.Extensions.Options;

namespace Jigen.TextEmbedding.Handlers;

public class CommandHandlers(IEmbeddingGenerator generator, IOptions<EmbeddingSettings> settings)
  : IRequestHandler<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings, float[]>
{
  public Task<float[]> Handle(CalculateEmbeddings request, CancellationToken cancellationToken)
  {
    return Task.FromResult(
      generator.GenerateEmbedding(request.Task ?? settings.Value.DefaultTask, request.Sentence));
  }
}