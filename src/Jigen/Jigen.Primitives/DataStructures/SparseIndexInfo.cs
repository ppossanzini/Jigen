namespace Jigen.DataStructures;

/// <summary>
/// Metrics of the lexical index of one collection. Every value is derived from
/// the current index state; none of them is persisted independently.
/// </summary>
public class SparseIndexInfo
{
  /// <summary>Distinct terms currently indexed.</summary>
  public int Terms { get; set; }

  /// <summary>Total (term, entry) pairs currently indexed.</summary>
  public long Postings { get; set; }

  /// <summary>Average length of a term list, zero when there are no terms.</summary>
  public double AveragePostingsPerTerm { get; set; }

  /// <summary>Estimated bytes held by the index structures.</summary>
  public long MemoryBytes { get; set; }

  /// <summary>Terms whose term list became empty and were dropped.</summary>
  public int RetiredTerms { get; set; }

  /// <summary>True once the collection index has been built from the archive.</summary>
  public bool Built { get; set; }
}
