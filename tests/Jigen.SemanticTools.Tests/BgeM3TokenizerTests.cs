using System.Numerics.Tensors;

namespace Jigen.SemanticTools.Tests;

public sealed class BgeM3TokenizerTests
{
    private const string ModelDirectory = "/data/onnx/bge-m3";
    private const string Ticket = "continuo ticket: 00020294 il problema del parco di Via Vittorio Emanuele è la scarsa illuminazione, ci sono solamente due lampioni che illuminano solo una piccola parte del parco, lasciando molti spazi bui e pericolosi. noi genitori per tamponare un minimo questo disagio utilizziamo le torce dei cellulari, ma come potete ben capire il problema è grave e anche di facile soluzione. allego anche fotografia, scattata intorno alle 17.2";

    [Fact]
    public void GenerateEmbedding_MatchesOfficialTokenizerJsonSimilarity()
    {
        var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.json");
        var modelPath = Path.Combine(ModelDirectory, "model.onnx");
        Assert.True(File.Exists(tokenizerPath), $"BGE-M3 tokenizer not found at '{tokenizerPath}'.");
        Assert.True(File.Exists(modelPath), $"BGE-M3 model not found at '{modelPath}'.");

        using var generator = new OnnxEmbeddingGenerator(
          tokenizerPath,
          modelPath,
          options: new EmbeddingGeneratorOptions
          {
              Profile = EmbeddingModelProfile.BgeM3,
              MaxTokens = 8192,
              UseChunking = false,
              OutputDimension = 1024,
              PaddingTokenId = 1,
              MaxBatchSize = 1
          });

        var ticketEmbedding = generator.GenerateEmbedding(Ticket);
        var queryEmbedding = generator.GenerateEmbedding("prostituzuione");
        var similarity = TensorPrimitives.CosineSimilarity(ticketEmbedding, queryEmbedding);

        Assert.InRange(similarity, 0.33f, 0.34f);
    }
}
