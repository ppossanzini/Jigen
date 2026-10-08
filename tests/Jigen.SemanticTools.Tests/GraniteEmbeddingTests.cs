using System.Numerics.Tensors;

namespace Jigen.SemanticTools.Tests;

public sealed class GraniteEmbeddingTests
{
  private const string Report = "Nel parco di Via Vittorio Emanuele ci sono pochi lampioni e molte zone restano buie e pericolose.";

  public static TheoryData<string, int> Models => new()
  {
    { "granite-embedding-97m-multilingual-r2", 384 },
    { "granite-embedding-311m-multilingual-r2", 768 }
  };

  [Theory]
  [MemberData(nameof(Models))]
  public void TokenizerJson_CountsItalianText(string modelDirectory, int outputDimension)
  {
    using var generator = CreateGenerator(modelDirectory, outputDimension);

    Assert.InRange(generator.CountTokens(Report), 10, 100);
  }

  [Theory]
  [MemberData(nameof(Models))]
  public void GenerateEmbedding_ReturnsFiniteNormalizedVector(string modelDirectory, int outputDimension)
  {
    using var generator = CreateGenerator(modelDirectory, outputDimension);

    var embedding = generator.GenerateEmbedding(Report);

    Assert.Equal(outputDimension, embedding.Length);
    Assert.All(embedding, value => Assert.True(float.IsFinite(value)));
    Assert.InRange(TensorPrimitives.Norm(embedding), 0.999f, 1.001f);
  }

  [Theory]
  [MemberData(nameof(Models))]
  public void GenerateEmbedding_RanksRelatedItalianQueryAboveUnrelatedText(string modelDirectory, int outputDimension)
  {
    using var generator = CreateGenerator(modelDirectory, outputDimension);

    var reportEmbedding = generator.GenerateEmbedding(Report);
    var relatedEmbedding = generator.GenerateEmbedding("illuminazione insufficiente e zone buie nel parco");
    var unrelatedEmbedding = generator.GenerateEmbedding("ricetta per preparare una torta al cioccolato");

    var relatedSimilarity = TensorPrimitives.CosineSimilarity(reportEmbedding, relatedEmbedding);
    var unrelatedSimilarity = TensorPrimitives.CosineSimilarity(reportEmbedding, unrelatedEmbedding);

    Assert.True(
      relatedSimilarity > unrelatedSimilarity,
      $"Expected related similarity {relatedSimilarity:F4} to exceed unrelated similarity {unrelatedSimilarity:F4}.");
  }

  [Theory]
  [MemberData(nameof(Models))]
  public void GenerateEmbeddings_ReturnsOneNormalizedVectorPerInput(string modelDirectory, int outputDimension)
  {
    using var generator = CreateGenerator(modelDirectory, outputDimension);

    var embeddings = generator.GenerateEmbeddings([
      Report,
      "La strada presenta una buca profonda vicino al marciapiede.",
      "Il contenitore dei rifiuti è pieno da diversi giorni."
    ]);

    Assert.Equal(3, embeddings.Length);
    Assert.All(embeddings, embedding =>
    {
      Assert.Equal(outputDimension, embedding.Length);
      Assert.InRange(TensorPrimitives.Norm(embedding), 0.999f, 1.001f);
    });
  }

  private static OnnxEmbeddingGenerator CreateGenerator(string modelDirectory, int outputDimension)
  {
    var basePath = Path.Combine("/data/onnx", modelDirectory);
    var tokenizerPath = Path.Combine(basePath, "tokenizer.json");
    var modelPath = Path.Combine(basePath, "onnx", "model_quint8_avx2.onnx");

    Assert.True(File.Exists(tokenizerPath), $"Tokenizer not found at '{tokenizerPath}'.");
    Assert.True(File.Exists(modelPath), $"Model not found at '{modelPath}'.");

    return new OnnxEmbeddingGenerator(
      tokenizerPath,
      modelPath,
      options: new EmbeddingGeneratorOptions
      {
        Profile = EmbeddingModelProfile.Granite,
        MaxTokens = 2048,
        UseChunking = false,
        OutputDimension = outputDimension,
        PaddingTokenId = 0,
        MaxBatchSize = 1
      });
  }
}