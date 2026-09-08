using Hikyaku;

namespace Jigen.Embedding.Core.Commands;

/// <summary>
/// Computes the same text in multiple independent embedding spaces.
/// </summary>
public class CalculateEmbeddingsMulti : IRequest<Dictionary<string, float[]>>
{
  public string Sentence { get; set; }
  public string Task { get; set; }
  public string[] Models { get; set; }
}
