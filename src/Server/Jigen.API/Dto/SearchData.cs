namespace Jigen.API.Dto;

public class SearchData
{
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; } = 10;

  /// <summary>
  /// Sparse half of the query: term identifier to weight. When present the
  /// search is hybrid and the two halves are fused by reciprocal rank.
  /// </summary>
  public Dictionary<int, float> SparseQuery { get; set; }

  /// <summary>
  /// Derive the sparse half from <see cref="Sentence"/> on the server, with the
  /// same tokenizer that produces the dense embedding: a hybrid search from a
  /// sentence alone. Ignored when <see cref="SparseQuery"/> is supplied.
  /// Defaults to true; an explicit false keeps the search dense-only.
  /// </summary>
  public bool SparseFromSentence { get; set; } = true;

  /// <summary>Drop dense hits below this similarity before the fusion.</summary>
  public float? MinDenseScore { get; set; }

  /// <summary>Drop sparse hits below this score before the fusion.</summary>
  public float? MinSparseScore { get; set; }
}
