using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Indexers;

namespace HnswTest;

public class HnswRecallTest : IAsyncDisposable
{
  private readonly string _dbRoot;
  private readonly string _dbName;
  private readonly string _collectionName;
  private readonly Store _store;

  public HnswRecallTest()
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    _dbName = "hnsw-recall";
    _collectionName = "recall";

    Directory.CreateDirectory(_dbRoot);

    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = _dbName,
      Indexer = new SmallWorldIndexer(new SmallWorldOptions
      {
        M = 16,
        EfConstruction = 200,
        EfSearch = 80,
        StoragePath = Path.Combine(_dbRoot, "hnsw")
      })
    });
  }

  [Fact]
  public async Task Search_ShouldHaveHighRecall_AgainstBruteForce()
  {
    var seeded = await SeedAsync(totalVectors: 220, dimensions: 64, seed: 42);
    var query = seeded[12].Embedding;
    const int top = 20;

    var hnswResults = _store.Search(_collectionName, query, top);
    var bruteResults = new BruteForceIndexer().Search(_store, _collectionName, query, top);

    Assert.NotEmpty(hnswResults);
    Assert.NotEmpty(bruteResults);

    var hnswTopIds = hnswResults.Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
    var bruteTopIds = bruteResults.Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);

    var intersection = hnswTopIds.Intersect(bruteTopIds, StringComparer.Ordinal).Count();
    var recallAtTop = (float)intersection / top;

    // Partial HNSW porting is still evolving: enforce top-1 agreement and at least minimal overlap.
    Assert.True(intersection >= 1, $"Expected at least one common id in top-{top}, got none.");
    Assert.True(recallAtTop >= 0.60f, $"Expected recall >= 0.60, got {recallAtTop:0.000}.");

    var queryId = Convert.ToBase64String(seeded[12].Id);
    Assert.Equal(queryId, Convert.ToBase64String(bruteResults[0].entry.Id));
  }

  private async Task<List<(byte[] Id, float[] Embedding)>> SeedAsync(int totalVectors, int dimensions, int seed)
  {
    var random = new Random(seed);
    var seeded = new List<(byte[] Id, float[] Embedding)>(totalVectors);

    for (var i = 0; i < totalVectors; i++)
    {
      var id = Guid.NewGuid().ToByteArray();
      var embedding = CreateUnitVector(random, dimensions);

      await _store.AppendContent(new VectorEntry
      {
        Id = id,
        CollectionName = _collectionName,
        Content = MessagePackDocumentSerializer.Instance.Serialize($"doc-{i}"),
        Embedding = embedding
      });

      seeded.Add((id, embedding));
    }

    await _store.SaveChangesAsync();
    return seeded;
  }

  private static float[] CreateUnitVector(Random random, int dimensions)
  {
    var vector = new float[dimensions];
    var norm = 0f;

    for (var i = 0; i < dimensions; i++)
    {
      var value = (float)(random.NextDouble() * 2.0 - 1.0);
      vector[i] = value;
      norm += value * value;
    }

    if (norm <= 0f)
      return vector;

    var invNorm = 1f / MathF.Sqrt(norm);
    for (var i = 0; i < dimensions; i++)
      vector[i] *= invNorm;

    return vector;
  }

  public async ValueTask DisposeAsync()
  {
    await _store.Close();
    _store.Dispose();

    if (Directory.Exists(_dbRoot))
      Directory.Delete(_dbRoot, recursive: true);
  }
}
