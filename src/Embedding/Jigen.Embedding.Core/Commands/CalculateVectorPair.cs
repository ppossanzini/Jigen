using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Generates both the dense embedding and the sparse lexical vector of one
/// sentence in a single dispatch, so a write pays one hop instead of two.
/// </summary>
public class CalculateVectorPair : IRequest<VectorPairDto>
{
  public string Model { get; set; }
  public string Task { get; set; }
  public string Sentence { get; set; }
}
