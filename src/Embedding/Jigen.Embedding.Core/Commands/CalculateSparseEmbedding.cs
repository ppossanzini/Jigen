using Hikyaku;
using Jigen.Embedding.Core.Dto;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Generates the sparse lexical vector of a sentence. The server never derives
/// it from a stored document at search time: this is the write-side companion of
/// <see cref="CalculateEmbeddings"/>.
/// </summary>
public class CalculateSparseEmbedding : IRequest<SparseEmbeddingDto>
{
  public string Model { get; set; }
  public string Sentence { get; set; }
}
