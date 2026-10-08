using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

public class CalculatePassageEmbeddings : IRequest<PassageEmbedding[]>
{
  public string Model { get; set; }
  public string Task { get; set; }
  public string Sentence { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}
