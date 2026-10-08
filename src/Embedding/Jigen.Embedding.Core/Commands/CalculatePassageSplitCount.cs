using Hikyaku;

namespace Jigen.Embedding.Core.Commands;

public class CalculatePassageSplitCount : IRequest<int>
{
  public string Model { get; set; }
  public string Sentence { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}
