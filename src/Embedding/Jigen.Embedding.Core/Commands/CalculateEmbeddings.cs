using Hikyaku;

namespace Jigen.Embedding.Core.Commands;

public class CalculateEmbeddings: IRequest<float[]>
{
  public string Model { get; set; }
  public string Task { get; set; }
  public string Sentence { get; set; }
}
