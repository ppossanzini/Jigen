namespace Jigen.API.Dto;

/// <summary>
/// Body for POST {collection}/graph. Mirrors the GET query params plus an optional
/// query vector: a few hundred floats don't belong in a query string, so highlighting
/// a search result on the graph goes through this dedicated POST instead.
/// </summary>
public class GetCollectionGraphRequest
{
  public int Dimensions { get; set; } = 2;
  public int Limit { get; set; } = 2000;
  public int? Level { get; set; }
  public float[] QueryEmbedding { get; set; }
}
