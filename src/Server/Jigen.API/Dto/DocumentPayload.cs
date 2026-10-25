namespace Jigen.API.Dto;

public class DocumentPayload
{
  public object Payload { get; set; }
  public string Sentence { get; set; }

  /// <summary>Sparse (lexical) terms of the document, mapping term id to weight.</summary>
  public Dictionary<int, float> Sparse { get; set; }

  /// <summary>
  /// Derive the sparse terms from <see cref="Sentence"/> on the server, with the
  /// same tokenizer that produces the dense embedding. Ignored when
  /// <see cref="Sparse"/> is supplied.
  /// </summary>
  public bool SparseFromSentence { get; set; }
}
