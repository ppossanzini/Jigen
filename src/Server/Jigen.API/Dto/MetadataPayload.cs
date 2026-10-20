namespace Jigen.API.Dto;

/// <summary>Shared metadata content, serialized like document payloads.</summary>
public class MetadataPayload
{
  public object Payload { get; set; }
}

/// <summary>Association target for an existing embedding key.</summary>
public class MetadataAssociationPayload
{
  public int MetadataIndex { get; set; }
}

/// <summary>
/// Grouped search input. CandidateBudget is mandatory and caps the sum of
/// requested candidates across attempts; fewer than Top groups may be returned.
/// </summary>
public class MetadataSearchData
{
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; } = 10;
  public int CandidateBudget { get; set; }
  public int EfSearch { get; set; }
  public bool NoContent { get; set; }
  public float? MinScore { get; set; }
}
