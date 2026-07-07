using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Indexers;

namespace HnswTest;

/// <summary>
/// Stress test for concurrent graph construction: multiple index workers
/// insert into the SAME collection in parallel (per-node locking). A corrupt
/// graph shows up as unreachable nodes (top-1 misses) or wrong rankings.
/// </summary>
public class HnswConcurrentIngestionTest : IAsyncDisposable
{
  private readonly string _dbRoot;
  private readonly string _collectionName = "concurrent";
  private readonly Store _store;

  public HnswConcurrentIngestionTest()
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);

    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "concurrent-ingest",
      IndexerWorkers = 4, // force real intra-collection parallelism
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
  public async Task ConcurrentInserts_BuildASearchableGraph()
  {
    const int total = 3000;
    const int dimensions = 32;

    var random = new Random(17);
    var seeded = new List<(byte[] Id, float[] Embedding)>(total);
    for (var i = 0; i < total; i++)
      seeded.Add((Guid.NewGuid().ToByteArray(), CreateUnitVector(random, dimensions)));

    foreach (var (id, embedding) in seeded)
      await _store.AppendContent(new VectorEntry
      {
        Id = id,
        CollectionName = _collectionName,
        Content = MessagePackDocumentSerializer.Instance.Serialize("doc"),
        Embedding = embedding
      });

    await _store.SaveChangesAsync();

    // Every sampled vector must find itself as its own nearest neighbour:
    // an unreachable node means the concurrent wiring corrupted the graph.
    var selfMisses = 0;
    for (var i = 0; i < total; i += 10)
    {
      var results = _store.Search(_collectionName, seeded[i].Embedding, 1).ToList();
      if (results.Count == 0 || !results[0].entry.Id.SequenceEqual(seeded[i].Id))
        selfMisses++;
    }

    Assert.True(selfMisses <= 3, $"{selfMisses}/300 sampled vectors cannot find themselves: graph likely corrupted");

    // Recall against exact search must stay high.
    var brute = new BruteForceIndexer();
    var recallSum = 0d;
    const int queries = 50;
    const int top = 10;
    for (var i = 0; i < queries; i++)
    {
      var query = seeded[i * (total / queries)].Embedding;
      var expected = brute.Search(_store, _collectionName, query, top)
        .Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
      var got = _store.Search(_collectionName, query, top)
        .Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
      recallSum += (double)got.Intersect(expected, StringComparer.Ordinal).Count() / top;
    }

    var recall = recallSum / queries;
    Assert.True(recall >= 0.85, $"recall {recall:0.000} below 0.85 after concurrent build");
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

    if (norm <= 0f) return vector;

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
