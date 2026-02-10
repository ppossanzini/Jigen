namespace Jigen.SemanticTools;

public interface IEmbeddingGenerator
{
  float[] GenerateEmbedding(string input);
}