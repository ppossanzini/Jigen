using Jigen.SemanticTools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Tests for <see cref="OnnxImageEmbeddingGenerator"/> against the synthetic
/// fixture model (Assets/tiny_vision_model.onnx), whose CLS token equals the
/// per-channel mean of the normalized input over H,W. This makes the fixture an
/// independent oracle for the preprocessing pipeline (decode, resize, normalize
/// with the configured mean/std), the CLS extraction and the L2 normalization.
/// </summary>
public class OnnxImageEmbeddingGeneratorTests
{
  private const string FixtureFileName = "tiny_vision_model.onnx";

  private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
  private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];

  private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Assets", FixtureFileName);

  [Fact]
  public void GenerateImageEmbedding_ReturnsNormalizedCls_FromConfiguredPreprocessing()
  {
    const byte r = 100, g = 150, b = 200;
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);
    var image = CreateSolidPng(224, 224, r, g, b);

    var embedding = generator.GenerateImageEmbedding(image);

    // The fixture returns the per-channel means of the normalized input as CLS:
    // expected = L2Normalize([(r/255-mean0)/std0, (g/255-mean1)/std1, (b/255-mean2)/std2])
    AssertClose(embedding, ExpectedCls(r, g, b));
  }

  [Fact]
  public void GenerateImageEmbedding_OutputIsUnitL2Norm()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);
    var image = CreateSolidPng(224, 224, 200, 40, 90);

    var embedding = generator.GenerateImageEmbedding(image);

    var norm = Math.Sqrt(embedding.Sum(value => value * value));
    Assert.Equal(1d, norm, 3);
  }

  [Fact]
  public void GenerateImageEmbedding_IsDeterministic_WithinUlpTolerance()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);
    var image = CreateSolidPng(224, 224, 33, 77, 122);

    var first = generator.GenerateImageEmbedding(image);
    var second = generator.GenerateImageEmbedding(image);

    // ONNX Runtime reduction kernels combine partial sums in a non-deterministic
    // order between runs, so identical inputs can differ by ~1 ULP (~6e-8 on
    // values ~0.9). The 1e-5 tolerance absorbs that noise while still catching
    // any real regression in preprocessing or extraction.
    AssertClose(first, second, tolerance: 1e-5f);
  }

  [Fact]
  public void GenerateImageEmbedding_ResizesNonStandardInput()
  {
    const byte r = 10, g = 220, b = 60;
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);

    // Solid color is invariant under the bicubic stretch, so a non-224 input
    // must produce the same embedding as a 224x224 one.
    var small = generator.GenerateImageEmbedding(CreateSolidPng(64, 48, r, g, b));
    var standard = generator.GenerateImageEmbedding(CreateSolidPng(224, 224, r, g, b));

    AssertClose(small, standard);
  }

  [Fact]
  public void GenerateImageEmbeddings_Batched_MatchesSingleCalls()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath, null,
      new ImageEmbeddingGeneratorOptions { MaxBatchSize = 2 });

    var imageA = CreateSolidPng(224, 224, 100, 150, 200);
    var imageB = CreateSolidPng(224, 224, 5, 90, 250);

    var batched = generator.GenerateImageEmbeddings([imageA, imageB]);
    var singleA = generator.GenerateImageEmbedding(imageA);
    var singleB = generator.GenerateImageEmbedding(imageB);

    Assert.Equal(2, batched.Length);
    AssertClose(batched[0], singleA);
    AssertClose(batched[1], singleB);
  }

  [Fact]
  public void GenerateImageEmbedding_FromPath_MatchesBytesOverload()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);
    var bytes = CreateSolidPng(224, 224, 77, 88, 99);

    var tempFile = Path.Combine(Path.GetTempPath(), $"jigen-test-{Guid.NewGuid():N}.png");
    try
    {
      File.WriteAllBytes(tempFile, bytes);
      AssertClose(generator.GenerateImageEmbedding(tempFile), generator.GenerateImageEmbedding(bytes));
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Fact]
  public async Task AsyncOverloads_MatchSyncResults()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);
    var image = CreateSolidPng(224, 224, 12, 34, 56);

    var tempFile = Path.Combine(Path.GetTempPath(), $"jigen-async-{Guid.NewGuid():N}.png");
    try
    {
      File.WriteAllBytes(tempFile, image);

      var sync = generator.GenerateImageEmbedding(image);
      var asyncBytes = await generator.GenerateImageEmbeddingAsync(image);
      var asyncPath = await generator.GenerateImageEmbeddingAsync(tempFile);

      AssertClose(sync, asyncBytes);
      AssertClose(sync, asyncPath);

      var batch = await generator.GenerateImageEmbeddingsAsync([image, image]);
      Assert.Equal(2, batch.Length);
      AssertClose(batch[0], sync);
      AssertClose(batch[1], sync);
    }
    finally
    {
      File.Delete(tempFile);
    }
  }

  [Fact]
  public void Constructor_Throws_OnInvalidArguments()
  {
    Assert.Throws<ArgumentException>(() => new OnnxImageEmbeddingGenerator(""));
    Assert.Throws<ArgumentException>(() => new OnnxImageEmbeddingGenerator("   "));
    Assert.Throws<ArgumentException>(() => new OnnxImageEmbeddingGenerator(
      FixturePath,
      null,
      new ImageEmbeddingGeneratorOptions { ImageMean = new float[] { 1f } }));
    Assert.Throws<ArgumentException>(() => new OnnxImageEmbeddingGenerator(
      FixturePath,
      null,
      new ImageEmbeddingGeneratorOptions { ImageStd = new float[] { 1f, 2f } }));
  }

  [Fact]
  public void GenerateImageEmbedding_Throws_OnInvalidInputs()
  {
    using var generator = new OnnxImageEmbeddingGenerator(FixturePath);

    Assert.Throws<ArgumentNullException>(() => generator.GenerateImageEmbedding((byte[])null));
    Assert.Throws<ArgumentException>(() => generator.GenerateImageEmbedding(Array.Empty<byte>()));
    Assert.Throws<ArgumentNullException>(() => generator.GenerateImageEmbeddings(null));
    Assert.Throws<ArgumentException>(() => generator.GenerateImageEmbedding((string)null));
  }

  // --- helpers -------------------------------------------------------------

  private static float[] ExpectedCls(byte r, byte g, byte b)
  {
    byte[] rgb = [r, g, b];
    var cls = new float[3];
    for (var i = 0; i < 3; i++)
      cls[i] = (rgb[i] / 255f - Mean[i]) / Std[i];
    return L2Normalize(cls);
  }

  private static float[] L2Normalize(float[] vector)
  {
    var normSquared = vector.Sum(value => value * value);
    if (normSquared <= 0f)
      return vector;

    var invNorm = 1f / MathF.Sqrt(normSquared);
    for (var i = 0; i < vector.Length; i++)
      vector[i] *= invNorm;
    return vector;
  }

  private static void AssertClose(float[] actual, float[] expected, float tolerance = 1e-3f)
  {
    Assert.Equal(expected.Length, actual.Length);
    for (var i = 0; i < expected.Length; i++)
    {
      var delta = Math.Abs(actual[i] - expected[i]);
      Assert.True(delta <= tolerance, $"Index {i}: expected {expected[i]}, got {actual[i]} (delta {delta}).");
    }
  }

  private static byte[] CreateSolidPng(int width, int height, byte r, byte g, byte b)
  {
    using var image = new Image<Rgba32>(width, height);
    image.Mutate(context => context.BackgroundColor(new Rgba32(r, g, b, 255)));

    using var stream = new MemoryStream();
    image.SaveAsPng(stream);
    return stream.ToArray();
  }
}
