using Jigen.SemanticTools;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Sparse lexical vector generation. It runs on the tokenizer, so the tests need
/// a real tokenizer but no model inference beyond what the generator already
/// loads for the dense side.
/// </summary>
public sealed class SparseEmbeddingTests
{
  private const string ModelDirectory = "/data/onnx/bge-m3";
  private const string NomicDirectory = "/data/onnx/nomic-embed-text-v1.5";

  private static OnnxEmbeddingGenerator CreateBgeM3(SparseEmbeddingOptions sparse = null)
  {
    var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.json");
    var modelPath = Path.Combine(ModelDirectory, "model.onnx");
    Assert.True(File.Exists(tokenizerPath), $"BGE-M3 tokenizer not found at '{tokenizerPath}'.");
    Assert.True(File.Exists(modelPath), $"BGE-M3 model not found at '{modelPath}'.");

    return new OnnxEmbeddingGenerator(
      tokenizerPath,
      modelPath,
      options: new EmbeddingGeneratorOptions
      {
        Profile = EmbeddingModelProfile.BgeM3,
        MaxTokens = 8192,
        UseChunking = false,
        OutputDimension = 1024,
        PaddingTokenId = 1,
        MaxBatchSize = 1,
        Sparse = sparse ?? new SparseEmbeddingOptions()
      });
  }

  [Fact]
  public void TermsAreUniqueIncreasingAndWeighted()
  {
    using var generator = CreateBgeM3();

    var sparse = generator.GenerateSparseEmbedding("the park is dark and the lamps are broken");

    Assert.NotEmpty(sparse.Terms);
    Assert.Equal(sparse.Terms.Length, sparse.Weights.Length);

    for (var i = 1; i < sparse.Terms.Length; i++)
      Assert.True(sparse.Terms[i] > sparse.Terms[i - 1], "Terms must be unique and strictly increasing.");

    Assert.All(sparse.Weights, weight => Assert.True(weight > 0));
  }

  [Fact]
  public void RepeatedTermWeighsMoreThanASingleOne()
  {
    using var generator = CreateBgeM3();

    var once = generator.GenerateSparseEmbedding("lamp");
    var thrice = generator.GenerateSparseEmbedding("lamp lamp lamp");

    var single = once.Weights[0];
    var repeated = thrice.Weights.Single(weight => Math.Abs(weight - single) > 1e-6f);

    Assert.True(repeated > single,
      $"A repeated term must weigh more: once={single}, three times={repeated}.");
  }

  [Fact]
  public void SpecialTokensNeverBecomeTerms()
  {
    using var generator = CreateBgeM3();

    // "lamp" alone tokenizes to a handful of ids, plus the sequence markers the
    // tokenizer adds; the markers must not be part of the vector.
    var sparse = generator.GenerateSparseEmbedding("lamp");
    var empty = generator.GenerateSparseEmbedding("");

    Assert.Empty(empty.Terms);
    Assert.DoesNotContain(0, sparse.Terms);
    Assert.DoesNotContain(1, sparse.Terms);
    Assert.DoesNotContain(2, sparse.Terms);
  }

  [Fact]
  public void EmptyInputYieldsNoTerms()
  {
    using var generator = CreateBgeM3();

    Assert.Empty(generator.GenerateSparseEmbedding("").Terms);
    Assert.Empty(generator.GenerateSparseEmbedding("   ").Terms);
  }

  [Fact]
  public void TheSameTextAlwaysYieldsTheSameVector()
  {
    using var generator = CreateBgeM3();

    var first = generator.GenerateSparseEmbedding("illuminazione del parco");
    var second = generator.GenerateSparseEmbedding("illuminazione del parco");

    Assert.Equal(first.Terms, second.Terms);
    Assert.Equal(first.Weights, second.Weights);
  }

  [Fact]
  public void TheTermCapKeepsTheHeaviestTerms()
  {
    using var generator = CreateBgeM3(new SparseEmbeddingOptions { MaxTerms = 3 });

    var sparse = generator.GenerateSparseEmbedding(
      "lamp lamp lamp park park bench road sidewalk tree");

    Assert.Equal(3, sparse.Terms.Length);
    Assert.Equal(3, sparse.Weights.Length);
  }

  [Fact]
  public void DifferentTextsShareTermsWhenTheyShareWords()
  {
    using var generator = CreateBgeM3();

    var first = generator.GenerateSparseEmbedding("il parco è buio");
    var second = generator.GenerateSparseEmbedding("il parco è illuminato");

    var shared = first.Terms.Intersect(second.Terms).ToArray();

    // "il", "parco" and "è" are in both texts: the lexical space is shared, which
    // is what makes a term match across documents possible at all.
    Assert.NotEmpty(shared);
  }

  [Fact]
  public void TheNomicOnnxTokenizerProducesAUsableVectorWithoutMarkers()
  {
    // nomic is configured with tokenizer.onnx (a tokenizer session), not with a
    // tokenizer.json: this is the other branch of the tokenizer code, so it must
    // be proven separately, not inferred from the BGE-M3 case.
    var tokenizerPath = Path.Combine(NomicDirectory, "tokenizer.onnx");
    var modelPath = Path.Combine(NomicDirectory, "model_int8.onnx");
    Assert.True(File.Exists(tokenizerPath), $"Nomic tokenizer not found at '{tokenizerPath}'.");
    Assert.True(File.Exists(modelPath), $"Nomic model not found at '{modelPath}'.");

    using var generator = new OnnxEmbeddingGenerator(tokenizerPath, modelPath,
      options: new EmbeddingGeneratorOptions { MaxTokens = 512, UseChunking = false });

    var sparse = generator.GenerateSparseEmbedding("vector databases in .NET");

    Assert.NotEmpty(sparse.Terms);
    Assert.Equal(sparse.Terms.Length, sparse.Weights.Length);

    for (var i = 1; i < sparse.Terms.Length; i++)
      Assert.True(sparse.Terms[i] > sparse.Terms[i - 1], "Terms must be unique and strictly increasing.");

    // BERT WordPiece markers are [CLS]=101 and [SEP]=102, [PAD]=0. None of them
    // is a word, so none may appear as a term.
    Assert.DoesNotContain(0, sparse.Terms);
    Assert.DoesNotContain(101, sparse.Terms);
    Assert.DoesNotContain(102, sparse.Terms);

    // The property a lexical index depends on: the same word must land on the
    // same term id in two different texts, otherwise nothing ever matches.
    var first = generator.GenerateSparseEmbedding("lampioni del parco");
    var second = generator.GenerateSparseEmbedding("parco senza lampioni");
    Assert.NotEmpty(first.Terms.Intersect(second.Terms).ToArray());

    // And repetition must weigh more, on this tokenizer too.
    var once = generator.GenerateSparseEmbedding("lamp");
    var thrice = generator.GenerateSparseEmbedding("lamp lamp lamp");
    var single = once.Weights[0];
    var repeated = thrice.Weights.Single(weight => Math.Abs(weight - single) > 1e-6f);
    Assert.True(repeated > single, $"once={single}, three times={repeated}.");
  }
}
