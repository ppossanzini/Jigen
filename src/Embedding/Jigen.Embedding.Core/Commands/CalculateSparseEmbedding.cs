using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Generates the sparse lexical vector of a sentence: the tokenizer terms of the
/// same text that produces the dense embedding.
/// </summary>
public class CalculateSparseEmbedding : IRequest<SparseEmbeddingDto>
{
  public string Model { get; set; }
  public string Sentence { get; set; }
}
