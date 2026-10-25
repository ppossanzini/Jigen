using Hikyaku;
using Jigen.DataStructures;

namespace Jigen.Core.Command.collections;

public class AppendDocument :  IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public string Sentence { get; set; }
  /// <summary>Every vector of the entry, all with the collection dimensions. A single-vector entry is an array of one.</summary>
  public float[][] Vectors { get; set; }
  /// <summary>Sparse (lexical) terms of the document; null when it has none.</summary>
  public SparseTerm[] SparseTerms { get; set; }

  /// <summary>
  /// Derive the sparse terms from <see cref="Sentence"/> with the same tokenizer
  /// that produces the dense embedding. Ignored when <see cref="SparseTerms"/> is
  /// supplied.
  /// </summary>
  public bool SparseFromSentence { get; set; }
}