using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

/// <summary>One pair per input sentence, in the same order.</summary>
public class CalculateVectorPairBatch : IRequest<VectorPairDto[]>
{
  public string Model { get; set; }
  public string Task { get; set; }
  public string[] Sentences { get; set; }
}
