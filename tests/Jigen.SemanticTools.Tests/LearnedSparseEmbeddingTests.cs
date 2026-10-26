using System.Buffers.Binary;
using System.IO.Compression;
using Jigen.SemanticTools;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Learned sparse weights (BGE-M3 sparse head). The real <c>sparse_linear.pt</c>
/// is not shipped with the model export, so the tests write a file with the same
/// layout PyTorch produces and choose weights whose outcome is known: with a zero
/// weight vector every content token scores exactly the bias.
/// </summary>
public sealed class LearnedSparseEmbeddingTests : IDisposable
{
  private const string ModelDirectory = "/data/onnx/bge-m3";
  private const int HiddenSize = 1024;

  private readonly string _directory = Path.Combine(Path.GetTempPath(), $"jigen-sparse-head-{Guid.CreateVersion7():N}");

  public LearnedSparseEmbeddingTests() => Directory.CreateDirectory(_directory);

  public void Dispose()
  {
    if (Directory.Exists(_directory))
      Directory.Delete(_directory, recursive: true);
  }

  private string WriteHead(float[] weight, float bias, string name = "sparse_linear.pt")
  {
    var path = Path.Combine(_directory, name);
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

    void Write(string entryName, byte[] bytes)
    {
      using var stream = archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
      stream.Write(bytes);
    }

    // data.pkl is never interpreted: the storages are recognised by their size.
    Write("sparse_linear/data.pkl", [0x80, 0x02, 0x7D, 0x2E]);
    Write("sparse_linear/byteorder", "little"u8.ToArray());
    Write("sparse_linear/data/0", ToBytes(weight));
    Write("sparse_linear/data/1", ToBytes([bias]));
    Write("sparse_linear/version", "3\n"u8.ToArray());
    return path;
  }

  private static byte[] ToBytes(float[] values)
  {
    var bytes = new byte[values.Length * sizeof(float)];
    for (var i = 0; i < values.Length; i++)
      BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), values[i]);
    return bytes;
  }

  private static OnnxEmbeddingGenerator CreateBgeM3(SparseEmbeddingOptions sparse)
  {
    var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.json");
    var modelPath = Path.Combine(ModelDirectory, "model.onnx");
    Assert.True(File.Exists(modelPath), $"BGE-M3 model not found at '{modelPath}'.");

    return new OnnxEmbeddingGenerator(tokenizerPath, modelPath,
      options: new EmbeddingGeneratorOptions
      {
        Profile = EmbeddingModelProfile.BgeM3,
        MaxTokens = 8192,
        UseChunking = false,
        OutputDimension = 1024,
        PaddingTokenId = 1,
        Sparse = sparse
      });
  }

  [Fact]
  public void TheHeadIsReadFromAPyTorchStateDict()
  {
    var weight = Enumerable.Range(0, HiddenSize).Select(i => i / 1000f).ToArray();
    var head = LearnedSparseHead.Load(WriteHead(weight, 0.5f));

    Assert.Equal(HiddenSize, head.HiddenSize);

    var hidden = new float[HiddenSize];
    hidden[10] = 2f;
    Assert.Equal(0.5f + 0.010f * 2f, head.Score(hidden), 5);

    // relu: a negative logit is "not a term".
    hidden[10] = -1000f;
    Assert.Equal(0f, head.Score(hidden));
  }

  [Fact]
  public void AFileWithoutTheTwoStoragesIsRejected()
  {
    var path = Path.Combine(_directory, "broken.pt");
    using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
    using (var stream = archive.CreateEntry("broken/data/0").Open())
      stream.Write(ToBytes(new float[HiddenSize]));

    Assert.Throws<InvalidDataException>(() => LearnedSparseHead.Load(path));
  }

  [Fact]
  public void LearnedTermsAreTheContentTokensWeightedByTheHead()
  {
    var headPath = WriteHead(new float[HiddenSize], bias: 1f);
    using var lexical = CreateBgeM3(new SparseEmbeddingOptions());
    using var learned = CreateBgeM3(new SparseEmbeddingOptions { HeadPath = headPath });

    const string text = "il parco è buio e i lampioni del parco sono rotti";
    var expected = lexical.GenerateSparseEmbedding(text);
    var sparse = learned.GenerateSparseEmbedding(text);

    Assert.True(learned.HasLearnedSparseHead);
    // Same vocabulary terms as the lexical side, markers excluded...
    Assert.Equal(expected.Terms, sparse.Terms);
    // ...but the weight is the head's, not the frequency: "parco" twice still scores 1.
    Assert.All(sparse.Weights, weight => Assert.Equal(1f, weight, 5));
  }

  [Fact]
  public void ARelativeHeadPathIsResolvedAgainstTheModelDirectory()
  {
    // The model directory is read-only test data, so the path climbs out of it.
    var headPath = WriteHead(new float[HiddenSize], bias: 1f);
    var relative = Path.GetRelativePath(ModelDirectory, headPath);

    using var learned = CreateBgeM3(new SparseEmbeddingOptions { HeadPath = relative });

    Assert.True(learned.HasLearnedSparseHead);
    Assert.NotEmpty(learned.GenerateSparseEmbedding("lampioni").Terms);
  }

  [Fact]
  public void TokensTheHeadScoresAtZeroAreDropped()
  {
    var headPath = WriteHead(new float[HiddenSize], bias: -1f);
    using var learned = CreateBgeM3(new SparseEmbeddingOptions { HeadPath = headPath });

    Assert.Empty(learned.GenerateSparseEmbedding("il parco è buio").Terms);
  }

  [Fact]
  public void TheTermCapAppliesToLearnedWeights()
  {
    var headPath = WriteHead(new float[HiddenSize], bias: 1f);
    using var learned = CreateBgeM3(new SparseEmbeddingOptions { HeadPath = headPath, MaxTerms = 3 });

    var sparse = learned.GenerateSparseEmbedding("lamp park bench road sidewalk tree");

    Assert.Equal(3, sparse.Terms.Length);
  }

  [Fact]
  public void OneInferenceGivesTheSameDenseAndSparseAsTwoSeparateCalls()
  {
    var weight = Enumerable.Range(0, HiddenSize).Select(i => (i % 7 - 3) / 100f).ToArray();
    var headPath = WriteHead(weight, bias: 0.2f);
    using var learned = CreateBgeM3(new SparseEmbeddingOptions { HeadPath = headPath });

    const string text = "illuminazione pubblica del parco cittadino";
    var (dense, sparse) = learned.GenerateEmbeddingWithSparse(text);

    var expectedDense = learned.GenerateEmbedding(text);
    var expectedSparse = learned.GenerateSparseEmbedding(text);

    Assert.Equal(expectedDense.Length, dense.Length);
    for (var i = 0; i < dense.Length; i++)
      Assert.Equal(expectedDense[i], dense[i], 5);

    Assert.Equal(expectedSparse.Terms, sparse.Terms);
    for (var i = 0; i < sparse.Weights.Length; i++)
      Assert.Equal(expectedSparse.Weights[i], sparse.Weights[i], 5);
  }

  [Fact]
  public async Task TheQueueRunsLearnedSparseRequestsAndPairs()
  {
    var headPath = WriteHead(new float[HiddenSize], bias: 1f);
    using var queued = new QueuedEmbeddingGenerator(
      CreateBgeM3(new SparseEmbeddingOptions { HeadPath = headPath }),
      maxConcurrency: 1, queueCapacity: 8, enqueueTimeout: TimeSpan.FromSeconds(30), maxBatchSize: 4);

    const string text = "lampioni del parco";
    var sparse = await queued.GenerateSparseEmbeddingAsync(text);
    var (dense, pair) = await ((IEmbeddingGenerator)queued).GenerateEmbeddingWithSparseAsync(text);

    Assert.NotEmpty(sparse.Terms);
    Assert.Equal(sparse.Terms, pair.Terms);
    Assert.Equal(1024, dense.Length);
    Assert.Empty((await queued.GenerateSparseEmbeddingAsync("  ")).Terms);
  }

  [Fact]
  public void AHeadOnAProfileOtherThanBgeM3IsAConfigurationError()
  {
    const string nomic = "/data/onnx/nomic-embed-text-v1.5";
    var headPath = WriteHead(new float[HiddenSize], bias: 1f);

    Assert.Throws<InvalidOperationException>(() => new OnnxEmbeddingGenerator(
      Path.Combine(nomic, "tokenizer.onnx"), Path.Combine(nomic, "model_int8.onnx"),
      options: new EmbeddingGeneratorOptions { Sparse = new SparseEmbeddingOptions { HeadPath = headPath } }));
  }
}
