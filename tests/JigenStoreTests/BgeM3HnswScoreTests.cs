using System.Numerics.Tensors;
using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.SemanticTools;

namespace JigenTests;

public sealed class BgeM3HnswScoreTests : IAsyncDisposable
{
    private const string ModelDirectory = "/data/onnx/bge-m3";
    private const string CollectionName = "tickets";
    private const string Ticket = "continuo ticket: 00020294 il problema del parco di Via Vittorio Emanuele è la scarsa illuminazione, ci sono solamente due lampioni che illuminano solo una piccola parte del parco, lasciando molti spazi bui e pericolosi. noi genitori per tamponare un minimo questo disagio utilizziamo le torce dei cellulari, ma come potete ben capire il problema è grave e anche di facile soluzione. allego anche fotografia, scattata intorno alle 17.2";
    private const string Query = "prostituzuione";

    private readonly string _testDirectory;
    private Store _store;

    public BgeM3HnswScoreTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        _testDirectory = Path.Combine(repositoryRoot, ".temp", nameof(BgeM3HnswScoreTests), Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task Search_ReturnsDirectCosineSimilarity_WithoutScoreInflation()
    {
        var tokenizerPath = Path.Combine(ModelDirectory, "tokenizer.json");
        var modelPath = Path.Combine(ModelDirectory, "model.onnx");
        Assert.True(File.Exists(tokenizerPath), $"BGE-M3 tokenizer not found at '{tokenizerPath}'.");
        Assert.True(File.Exists(modelPath), $"BGE-M3 model not found at '{modelPath}'.");

        using var embeddingGenerator = new OnnxEmbeddingGenerator(
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

        var ticketEmbedding = embeddingGenerator.GenerateEmbedding(Ticket);
        var queryEmbedding = embeddingGenerator.GenerateEmbedding(Query);
        var directSimilarity = TensorPrimitives.CosineSimilarity(ticketEmbedding, queryEmbedding);

        _store = new Store(new StoreOptions
        {
            DataBaseName = "bge-m3-hnsw-score",
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

        var result = Assert.Single(_store.Search(CollectionName, queryEmbedding, top: 1));

        Assert.True(Math.Abs(result.score - directSimilarity) < 1e-4f,
          $"HNSW score {result.score:F6} differs from direct cosine similarity {directSimilarity:F6}.");
        Assert.True(directSimilarity is >= 0.33f and <= 0.34f,
          $"BGE-M3 produced an inflated similarity: expected about 0.3355, direct cosine was {directSimilarity:F4} and HNSW returned {result.score:F4}.");
        Assert.True(result.score < 0.5f,
          $"Unrelated query score was inflated: direct cosine was {directSimilarity:F4}, HNSW returned {result.score:F4}.");
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
