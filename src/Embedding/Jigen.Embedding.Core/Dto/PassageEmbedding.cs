namespace Jigen.Embedding.Core.Dto;

public class PassageEmbedding
{
    public string Text { get; set; }
    public int StartToken { get; set; }
    public int TokenCount { get; set; }
    public float[] Embedding { get; set; }
}
