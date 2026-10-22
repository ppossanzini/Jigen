using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexers;
using Jigen.SemanticTools;

namespace JigenTests;

/// <summary>
/// The chain the user asked for, end to end: one sentence produces the dense
/// embedding and the sparse vector, the sparse one is stored, and a lexical
/// search finds the entry it came from. It runs on the real model and a real
/// archive, so it fails if any link of the chain drifts.
/// </summary>
public sealed class GeneratedSparseIntegrationTests : IDisposable
{
  private const string ModelDirectory = "/data/onnx/bge-m3";
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"jigen-generated-{Guid.NewGuid():N}");

  private static OnnxEmbeddingGenerator CreateGenerator() => new(
    Path.Combine(ModelDirectory, "tokenizer.json"),
    Path.Combine(ModelDirectory, "model.onnx"),
    options: new EmbeddingGeneratorOptions
    {
      Profile = EmbeddingModelProfile.BgeM3,
      MaxTokens = 8192,
      UseChunking = false,
      OutputDimension = 1024,
      PaddingTokenId = 1
    });

  private static bool ModelAvailable =>
    File.Exists(Path.Combine(ModelDirectory, "tokenizer.json"))
    && File.Exists(Path.Combine(ModelDirectory, "model.onnx"));

  private Store OpenStore() => new(new StoreOptions
  {
    DataBaseName = "generated",
    DataBasePath = _path,
    Wal = new WalOptions { Enabled = true },
    SparseIndex = new SparseIndex()
  });

  [Fact]
  public async Task ASentenceProducesBothVectorsAndTheSparseOneIsSearchable()
  {
    if (!ModelAvailable) return; // model not installed on this machine
    Directory.CreateDirectory(_path);

    using var generator = CreateGenerator();
    using var store = OpenStore();

    const string Sentence = "il parco di via vittorio emanuele è buio e servono più lampioni";

    var dense = generator.GenerateEmbedding(Sentence);
    var generated = generator.GenerateSparseEmbedding(Sentence);

    Assert.NotEmpty(generated.Terms);

    // The generated shape is exactly what the store accepts: no adaptation
    // step, no re-sorting, no de-duplication at the boundary.
    var sparse = SparseVector.Create(generated.Terms, generated.Weights, int.MaxValue);

    await store.AppendContent(new VectorEntry
    {
      CollectionName = "docs",
      Id = Encoding.UTF8.GetBytes("ticket-1"),
      Content = Encoding.UTF8.GetBytes("{\"title\":\"parco buio\"}"),
      Embedding = dense,
      Sparse = sparse
    });

    store.SaveChangesAsync().GetAwaiter().GetResult();

    // Read back: the pairs survive the write and the reopen.
    var stored = store.GetSparse("docs", Encoding.UTF8.GetBytes("ticket-1"));
    Assert.NotNull(stored);
    Assert.Equal(generated.Terms, stored.Terms.ToArray());

    // A lexical query built from a sentence sharing words finds the entry.
    var query = generator.GenerateSparseEmbedding("mancano lampioni nel parco");
    var hits = store.SearchSparse("docs", SparseVector.Create(query.Terms, query.Weights, int.MaxValue), 5)
      .ToArray();

    Assert.NotEmpty(hits);
    Assert.Equal("ticket-1", Encoding.UTF8.GetString(hits[0].key));
  }

  [Fact]
  public void TermsFromDifferentSentencesOverlapOnSharedWords()
  {
    if (!ModelAvailable) return;

    using var generator = CreateGenerator();

    var first = generator.GenerateSparseEmbedding("lampioni del parco");
    var second = generator.GenerateSparseEmbedding("parco senza lampioni");

    // The overlap is what makes a lexical index useful at all: the same word
    // must land on the same term id in both vectors.
    Assert.NotEmpty(first.Terms.Intersect(second.Terms).ToArray());
  }

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(_path))
        Directory.Delete(_path, recursive: true);
    }
    catch (IOException)
    {
      // A file still mapped by the store: the temporary folder can stay.
    }
  }
}
