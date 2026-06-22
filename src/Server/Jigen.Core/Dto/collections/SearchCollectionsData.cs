namespace Jigen.Core.Dto.collections;

public class SearchCollectionsData
{
  public IEnumerable<string> Collections { get; set; }
  public string Sentence { get; set; }
  // public float[] Embeddings { get; set; }
  public int Top { get; set; }
}
