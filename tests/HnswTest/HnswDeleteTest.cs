using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;

namespace HnswTest;

public class HnswDeleteTest : IAsyncDisposable
{
  private readonly string _dbRoot;
  private readonly string _collectionName = "deletes";
  private readonly Store _store;

  public HnswDeleteTest()
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);

    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "hnsw-delete",
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
  public async Task Search_NeverReturnsDeletedEntries_EvenAfterHeavyDeletion()
  {
    var seeded = await SeedAsync(totalVectors: 120, dimensions: 32, seed: 7);

    // Delete every other entry: with skip-on-traversal semantics this
    // fragments the graph and makes survivors unreachable.
    var deleted = new HashSet<string>(StringComparer.Ordinal);
    for (var i = 0; i < seeded.Count; i += 2)
    {
      Assert.True(await _store.DeleteContent(_collectionName, seeded[i].Id));
      deleted.Add(Convert.ToBase64String(seeded[i].Id));
    }

    // Every survivor must still be findable as its own nearest neighbour,
    // and no deleted entry may ever surface.
    for (var i = 1; i < seeded.Count; i += 2)
    {
      var results = _store.Search(_collectionName, seeded[i].Embedding, 5).ToList();

      Assert.NotEmpty(results);
      Assert.All(results, r => Assert.DoesNotContain(Convert.ToBase64String(r.entry.Id), deleted));
      Assert.Equal(
        Convert.ToBase64String(seeded[i].Id),
        Convert.ToBase64String(results[0].entry.Id));
    }
  }

  [Fact]
  public async Task Search_SurvivesDeletingAlmostEverything_IncludingTheEntrypoint()
  {
    var seeded = await SeedAsync(totalVectors: 40, dimensions: 16, seed: 21);

    // Delete every entry but the last one: at some point the entrypoint
    // itself is deleted, whatever node holds that role.
    for (var i = 0; i < seeded.Count - 1; i++)
      Assert.True(await _store.DeleteContent(_collectionName, seeded[i].Id));

    var survivor = seeded[^1];
    var results = _store.Search(_collectionName, survivor.Embedding, 5).ToList();

    Assert.Single(results);
    Assert.Equal(Convert.ToBase64String(survivor.Id), Convert.ToBase64String(results[0].entry.Id));

    // Delete the survivor too: the collection must simply return nothing.
    Assert.True(await _store.DeleteContent(_collectionName, survivor.Id));
    Assert.Empty(_store.Search(_collectionName, survivor.Embedding, 5));
  }

  [Fact]
  public async Task Insert_IntoFullyDeletedGraph_MakesTheNewEntrySearchable()
  {
    var seeded = await SeedAsync(totalVectors: 15, dimensions: 16, seed: 3);

    foreach (var (id, _) in seeded)
      Assert.True(await _store.DeleteContent(_collectionName, id));

    // The graph now only contains deleted nodes: a fresh insert must become
    // the new entrypoint and be immediately searchable.
    var freshId = Guid.NewGuid().ToByteArray();
    var freshEmbedding = CreateUnitVector(new Random(99), 16);
    await _store.AppendContent(new VectorEntry
    {
      Id = freshId,
      CollectionName = _collectionName,
      Content = MessagePackDocumentSerializer.Instance.Serialize("fresh"),
      Embedding = freshEmbedding
    });
    await _store.SaveChangesAsync();

    var results = _store.Search(_collectionName, freshEmbedding, 3).ToList();
    Assert.Single(results);
    Assert.Equal(Convert.ToBase64String(freshId), Convert.ToBase64String(results[0].entry.Id));
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
