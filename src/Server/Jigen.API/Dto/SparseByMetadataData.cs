namespace Jigen.API.Dto;

/// <summary>
/// Grouped lexical search: one result per shared metadata record.
/// CandidateBudget is mandatory and caps the entries requested from the term
/// lists, so a group-rich collection cannot turn one query into a full scan.
/// </summary>
public class SparseByMetadataData
{
  public SparsePayload Sparse { get; set; }
  public int Top { get; set; } = 10;
  public int CandidateBudget { get; set; }

  /// <summary>Return keys and scores only, without the content.</summary>
  public bool NoContent { get; set; }
}
