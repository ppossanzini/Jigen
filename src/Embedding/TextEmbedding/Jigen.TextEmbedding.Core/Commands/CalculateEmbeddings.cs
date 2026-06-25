using Hikyaku;

namespace Jigen.TextEmbedding.Core.Commands;

public class CalculateEmbeddings: IRequest<float[]>
{
  public string Task { get; set; }
  public string Sentence { get; set; }
}