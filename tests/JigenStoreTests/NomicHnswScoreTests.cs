using System.Numerics.Tensors;
using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.SemanticTools;
using Xunit.Abstractions;

namespace JigenTests;

public sealed class NomicHnswScoreTests : IAsyncDisposable
{
    private const string ModelDirectory = "/data/onnx/nomic-embed-text-v1.5";
    private const string CollectionName = "tickets";
    private const string Ticket = "continuo ticket: 00020294 il problema del parco di Via Vittorio Emanuele è la scarsa illuminazione, ci sono solamente due lampioni che illuminano solo una piccola parte del parco, lasciando molti spazi bui e pericolosi. noi genitori per tamponare un minimo questo disagio utilizziamo le torce dei cellulari, ma come potete ben capire il problema è grave e anche di facile soluzione. allego anche fotografia, scattata intorno alle 17.2";

    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _testDirectory;
    private Store _store;

    public NomicHnswScoreTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var repositoryRoot = FindRepositoryRoot();
        _testDirectory = Path.Combine(repositoryRoot, ".temp", nameof(NomicHnswScoreTests), Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task Search_PreservesNomicCosineAndRanksRelevantQueryHigher()
    {
        var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.onnx");
        var modelPath = Path.Combine(ModelDirectory, "model_int8.onnx");
        Assert.True(File.Exists(tokenizerPath), $"Nomic tokenizer not found at '{tokenizerPath}'.");
        Assert.True(File.Exists(modelPath), $"Nomic model not found at '{modelPath}'.");

        using var embeddingGenerator = new OnnxEmbeddingGenerator(
          tokenizerPath,
          modelPath,
          options: new EmbeddingGeneratorOptions
          {
              Profile = EmbeddingModelProfile.Nomic,
              MaxTokens = 2048,
              UseChunking = true,
              ChunkSize = 1536,
              ChunkOverlap = 192,
              MaxBatchSize = 1
          });

        var ticketEmbedding = embeddingGenerator.GenerateEmbedding("search_document", Ticket);
        var unrelatedQueryEmbedding = embeddingGenerator.GenerateEmbedding("search_query", "prostituzuione");
        var relevantQueryEmbedding = embeddingGenerator.GenerateEmbedding("search_query", "scarsa illuminazione");
        var unrelatedSimilarity = TensorPrimitives.CosineSimilarity(ticketEmbedding, unrelatedQueryEmbedding);
        var relevantSimilarity = TensorPrimitives.CosineSimilarity(ticketEmbedding, relevantQueryEmbedding);

        _store = new Store(new StoreOptions
        {
            DataBaseName = "nomic-hnsw-score",
            DataBasePath = _testDirectory,
            Indexer = new SmallWorldIndexer(new SmallWorldOptions
            {
                M = 16,
                EfConstruction = 200,
                EfSearch = 64,
                StoragePath = Path.Combine(_testDirectory, "hnsw"),
                generator = new Random(20294)
            })
        });

        await _store.AppendContent(new VectorEntry
        {
            Id = Guid.CreateVersion7().ToByteArray(),
            CollectionName = CollectionName,
            Content = Encoding.UTF8.GetBytes(Ticket),
            Embedding = ticketEmbedding
        });
        await _store.SaveChangesAsync();

        var unrelatedResult = Assert.Single(_store.Search(CollectionName, unrelatedQueryEmbedding, top: 1));
        var relevantResult = Assert.Single(_store.Search(CollectionName, relevantQueryEmbedding, top: 1));

        _testOutputHelper.WriteLine($"prostituzuione: direct={unrelatedSimilarity:F6}, hnsw={unrelatedResult.score:F6}");
        _testOutputHelper.WriteLine($"scarsa illuminazione: direct={relevantSimilarity:F6}, hnsw={relevantResult.score:F6}");

        Assert.True(Math.Abs(unrelatedResult.score - unrelatedSimilarity) < 1e-4f,
          $"HNSW score {unrelatedResult.score:F6} differs from unrelated direct cosine {unrelatedSimilarity:F6}.");
        Assert.True(Math.Abs(relevantResult.score - relevantSimilarity) < 1e-4f,
          $"HNSW score {relevantResult.score:F6} differs from relevant direct cosine {relevantSimilarity:F6}.");
        Assert.True(relevantResult.score > unrelatedResult.score,
          $"Relevant query score {relevantResult.score:F4} must exceed unrelated query score {unrelatedResult.score:F4}.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_store is not null)
        {
            await _store.Close();
            _store.Dispose();
        }

        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Jigen repository root could not be located.");
    }
}
