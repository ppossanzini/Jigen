namespace Jigen.Core.Dto.collections;

public class SearchCollectionsData
{
  public IEnumerable<string> Collections { get; set; }
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; }

  /// <summary>Derive the sparse half from <see cref="Sentence"/> on the server. Defaults to true.</summary>
  public bool SparseFromSentence { get; set; } = true;

  /// <summary>Drop dense hits below this similarity.</summary>
  public float? MinDenseScore { get; set; }

  /// <summary>Drop sparse hits below this score.</summary>
  public float? MinSparseScore { get; set; }
}
