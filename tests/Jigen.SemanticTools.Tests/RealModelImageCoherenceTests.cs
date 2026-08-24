using System.Text;
using Jigen.SemanticTools;
using Xunit.Abstractions;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Coherence tests for the real <c>nomic-embed-vision-v1.5</c> model
/// (<c>model_int8.onnx</c>) against <c>nomic-embed-text-v1.5</c>: the vision
/// model is trained to project images into the text model's embedding space, so
/// the embedding of a real photo must be closer to text describing its content
/// than to unrelated text. Requires the ONNX models under <c>/data/onnx</c> —
/// the same convention used by <c>JigenStoreTests</c>.
/// </summary>
public class RealModelImageCoherenceTests
{
  private const string VisionModelPath = "/data/onnx/nomic-embed-vision-v1.5/model_int8.onnx";
  private const string TextTokenizerPath = "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx";
  private const string TextModelPath = "/data/onnx/nomic-embed-text-v1.5/model_int8.onnx";

  private const string ScreenshotFile = "beach-lounger-screenshot.png";

  private readonly ITestOutputHelper _output;

  public RealModelImageCoherenceTests(ITestOutputHelper output)
  {
    _output = output;
  }

  /// <summary>
  /// The screenshot depicts a beach lounger facing the sea. Its embedding must
  /// be closer (higher cosine) to beach/sea text than to unrelated text, and
  /// the gap must be meaningful — this is the cross-modal alignment the model
  /// was trained for.
  /// </summary>
  [Fact]
  public void BeachLoungerScreenshot_Embedding_IsCloserToBeachText_ThanToUnrelatedText()
  {
    var screenshotPath = Path.Combine(AppContext.BaseDirectory, "Assets", ScreenshotFile);
    Assert.True(File.Exists(screenshotPath), $"Missing screenshot asset: {screenshotPath}");

    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);

    var imageEmbedding = vision.GenerateImageEmbedding(File.ReadAllBytes(screenshotPath));

    // Sanity checks on the produced vector: 768-dim (ViT hidden size) and
    // L2-normalized (required for cosine against the text side).
    Assert.Equal(768, imageEmbedding.Length);
    Assert.Equal(1d, L2Norm(imageEmbedding), 3);
    Assert.True(imageEmbedding.Sum(v => v * v) > 0f, "Image embedding is degenerate (zero vector).");

    string[] related =
    [
      "a beach lounger facing the sea",
      "a sun lounger on the sand by the ocean",
      "a deck chair on a sandy beach",
      "a beach chair looking at the sea"
    ];

    string[] unrelated =
    [
      "a red sports car driving on a highway",
      "a cat sleeping on a sofa",
      "a laptop on a desk in an office",
      "spaghetti with tomato sauce on a plate"
    ];

    // Text queries use the search_query task prefix (query side of the pair),
    // exactly as documented for cross-modal retrieval.
    var relatedEmbeddings = related
      .Select(sentence => text.GenerateEmbedding("search_query", sentence))
      .ToArray();
    var unrelatedEmbeddings = unrelated
      .Select(sentence => text.GenerateEmbedding("search_query", sentence))
      .ToArray();

    Assert.All(relatedEmbeddings, embedding => Assert.Equal(1d, L2Norm(embedding), 3));
    Assert.All(unrelatedEmbeddings, embedding => Assert.Equal(1d, L2Norm(embedding), 3));

    var relatedCosines = relatedEmbeddings.Select(embedding => Cosine(imageEmbedding, embedding)).ToArray();
    var unrelatedCosines = unrelatedEmbeddings.Select(embedding => Cosine(imageEmbedding, embedding)).ToArray();

    // Keep the application's current default in the diagnostic output too.
    // Sentence-based searches that omit the task are embedded as
    // `search_document`, while retrieval queries must use `search_query`.
    var documentTaskRelatedCosines = related
      .Select(sentence => Cosine(imageEmbedding, text.GenerateEmbedding("search_document", sentence)))
      .ToArray();

    for (var i = 0; i < related.Length; i++)
      _output.WriteLine($"related   [{i}] {related[i],-42} cos = {relatedCosines[i]:F4}");
    for (var i = 0; i < unrelated.Length; i++)
      _output.WriteLine($"unrelated [{i}] {unrelated[i],-42} cos = {unrelatedCosines[i]:F4}");
    for (var i = 0; i < related.Length; i++)
      _output.WriteLine($"wrong task [{i}] {related[i],-42} cos = {documentTaskRelatedCosines[i]:F4}");

    var minRelated = relatedCosines.Min();
    var maxUnrelated = unrelatedCosines.Max();

    _output.WriteLine($"min(related)   = {minRelated:F4}");
    _output.WriteLine($"max(unrelated) = {maxUnrelated:F4}");
    _output.WriteLine($"mean related (search_query)    = {relatedCosines.Average():F4}");
    _output.WriteLine($"mean related (search_document) = {documentTaskRelatedCosines.Average():F4}");

    // Coherence gate: every related text must beat the most similar unrelated
    // text by a clear margin. A broken/garbage model fails this.
    const float requiredMargin = 0.02f;
    Assert.True(
      minRelated - maxUnrelated > requiredMargin,
      $"Image embedding not coherent with content: min related cosine {minRelated:F4} " +
      $"- max unrelated cosine {maxUnrelated:F4} = {minRelated - maxUnrelated:F4} <= {requiredMargin:F2}.");

    Assert.True(
      relatedCosines.Average() > documentTaskRelatedCosines.Average(),
      "Using search_document for a retrieval query unexpectedly outperformed search_query.");
  }

  /// <summary>
  /// The real vision model must be deterministic within ULP noise and always
  /// return a unit vector of the expected dimension.
  /// </summary>
  [Fact]
  public void VisionModel_IsDeterministic_AndReturnsUnitVectors()
  {
    var screenshotPath = Path.Combine(AppContext.BaseDirectory, "Assets", ScreenshotFile);
    Assert.True(File.Exists(screenshotPath), $"Missing screenshot asset: {screenshotPath}");

    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    var bytes = File.ReadAllBytes(screenshotPath);

    var first = vision.GenerateImageEmbedding(bytes);
    var second = vision.GenerateImageEmbedding(bytes);

    Assert.Equal(768, first.Length);
    Assert.Equal(first.Length, second.Length);
    Assert.Equal(1d, L2Norm(first), 3);
    Assert.Equal(1d, L2Norm(second), 3);

    var maxDelta = first.Zip(second, (a, b) => Math.Abs(a - b)).Max();
    _output.WriteLine($"max |Δ| between runs = {maxDelta:E2}");
    Assert.True(maxDelta < 1e-4f, $"Runs differ by {maxDelta:E2} — model output is not stable.");
  }

  // --- helpers -------------------------------------------------------------

  private static double L2Norm(float[] vector)
  {
    var normSquared = 0d;
    foreach (var value in vector)
      normSquared += (double)value * value;
    return Math.Sqrt(normSquared);
  }

  private static float Cosine(float[] a, float[] b)
  {
    // Keep the full formula as a defensive check even though both public
    // embedding paths guarantee unit vectors.
    var normA = (float)L2Norm(a);
    var normB = (float)L2Norm(b);
    if (normA <= 0f || normB <= 0f)
      return 0f;

    var dot = 0f;
    for (var i = 0; i < a.Length; i++)
      dot += a[i] * b[i];
    return dot / (normA * normB);
  }
}
