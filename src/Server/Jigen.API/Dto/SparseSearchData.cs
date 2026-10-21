namespace Jigen.API.Dto;

public class SparseSearchData
{
  public SparsePayload Sparse { get; set; }
  public int Top { get; set; } = 10;

  /// <summary>Return keys and scores only, without the content.</summary>
  public bool NoContent { get; set; }

  /// <summary>Drop results scoring below this value.</summary>
  public float? MinScore { get; set; }
}
