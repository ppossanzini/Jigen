using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

/// <summary>One sparse vector per input sentence, in the same order.</summary>
public class CalculateSparseEmbeddingBatch : IRequest<SparseEmbeddingDto[]>
{
  public string Model { get; set; }
  public string[] Sentences { get; set; }
}
