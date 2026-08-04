namespace Jigen.API.Dto;

public class SearchData
{
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; } = 10;
}
