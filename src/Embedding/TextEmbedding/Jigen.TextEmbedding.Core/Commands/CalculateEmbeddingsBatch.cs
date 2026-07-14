using Hikyaku;

namespace Jigen.TextEmbedding.Core.Commands;

/// <summary>
/// Batch counterpart of <see cref="CalculateEmbeddings"/>: one request (and,
/// with a remote embedding worker, one dispatch) for many sentences. The
/// result has one row per sentence, in the same order; a sentence the model
/// could not embed yields an empty row.
/// </summary>
public class CalculateEmbeddingsBatch : IRequest<float[][]>
{
  public string Task { get; set; }
  public string[] Sentences { get; set; }
}
