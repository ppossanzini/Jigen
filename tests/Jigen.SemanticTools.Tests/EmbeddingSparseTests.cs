namespace Jigen.SemanticTools.Tests;

/// <summary>
/// The sparse side of the embedding generator: tokenizer terms of the same text
/// that produces the dense embedding. No inference is involved, so these tests
/// only need the tokenizer and the model file the profile expects.
/// </summary>
public sealed class EmbeddingSparseTests
{
    private const string ModelDirectory = "/data/onnx/nomic-embed-text-v1.5";

    private static OnnxEmbeddingGenerator CreateGenerator()
    {
        var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.onnx");
        var modelPath = Path.Combine(ModelDirectory, "model_int8.onnx");
        Assert.True(File.Exists(tokenizerPath), $"Tokenizer not found at '{tokenizerPath}'.");
        Assert.True(File.Exists(modelPath), $"Model not found at '{modelPath}'.");

        return new OnnxEmbeddingGenerator(tokenizerPath, modelPath);
    }

    [Fact]
    public void GenerateSparseEmbedding_returns_ordered_positive_terms()
    {
        using var generator = CreateGenerator();

        var sparse = generator.GenerateSparseEmbedding("the quick brown fox jumps over the lazy dog");

        Assert.True(sparse.Count > 0);
        Assert.Equal(sparse.Count, sparse.Weights.Length);
        Assert.All(sparse.Weights, weight => Assert.True(weight > 0));

        for (var i = 1; i < sparse.Terms.Length; i++)
            Assert.True(sparse.Terms[i] > sparse.Terms[i - 1], "term ids must be strictly increasing");
    }

    [Fact]
    public void GenerateSparseEmbedding_is_deterministic_for_the_same_text()
    {
        using var generator = CreateGenerator();

        var first = generator.GenerateSparseEmbedding("deterministic text");
        var second = generator.GenerateSparseEmbedding("deterministic text");

        Assert.Equal(first.Terms, second.Terms);
        Assert.Equal(first.Weights, second.Weights);
    }

    [Fact]
    public void GenerateSparseEmbedding_returns_nothing_for_blank_text()
    {
        using var generator = CreateGenerator();

        Assert.Equal(0, generator.GenerateSparseEmbedding("").Count);
        Assert.Equal(0, generator.GenerateSparseEmbedding("   ").Count);
    }

    [Fact]
    public void GenerateSparseEmbedding_weighs_a_repeated_word_more()
    {
        using var generator = CreateGenerator();

        var once = generator.GenerateSparseEmbedding("fox");
        var thrice = generator.GenerateSparseEmbedding("fox fox fox");

        Assert.Equal(once.Terms, thrice.Terms);
        Assert.True(thrice.Weights[0] > once.Weights[0]);
    }

    [Fact]
    public void GenerateSparseEmbedding_honours_the_term_cap()
    {
        using var generator = new OnnxEmbeddingGenerator(
          Path.Combine(ModelDirectory, "tokenizer.onnx"),
          Path.Combine(ModelDirectory, "model_int8.onnx"),
          options: new EmbeddingGeneratorOptions { Sparse = new SparseEmbeddingOptions { MaxTerms = 3 } });

        var sparse = generator.GenerateSparseEmbedding("one two three four five six seven eight");

        Assert.Equal(3, sparse.Count);
    }

    [Fact]
    public void GenerateSparseEmbedding_keeps_the_dense_path_working()
    {
        using var generator = CreateGenerator();

        var dense = generator.GenerateEmbedding("the quick brown fox");

        Assert.Equal(768, dense.Length);
    }
}
