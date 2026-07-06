using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Persistance;

namespace HnswTest;

/// <summary>
/// Covers the split graph storage: immutable vectors ({name}.hnsw.vec,
/// append-only) and fixed-size adjacency records ({name}.hnsw.adj, in-place
/// updates), plus the one-time migration from the legacy single-file format.
/// </summary>
public class HnswSplitStorageTest
{
  private static string NewTempRoot() =>
    Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));

  private static StoreOptions OptionsFor(string root) => new()
  {
    DataBasePath = root,
    DataBaseName = "split-storage",
    Indexer = new SmallWorldIndexer(new SmallWorldOptions
    {
      M = 16,
      EfConstruction = 200,
      EfSearch = 80,
      StoragePath = Path.Combine(root, "hnsw"),
      generator = new Random(4242)
    })
  };

  [Fact]
  public async Task Graph_SurvivesCleanReopen_WithoutReconcile()
  {
    var root = NewTempRoot();
    Directory.CreateDirectory(root);

    try
    {
      var seeded = await SeedAsync(root, totalVectors: 60, dimensions: 16, seed: 5);

      var reopened = new Store(OptionsFor(root));
      Assert.False(reopened.WasUncleanShutdown); // no reconcile: pure storage roundtrip

      foreach (var probe in new[] { seeded[0], seeded[29], seeded[^1] })
      {
        var results = reopened.Search("docs", probe.Embedding, 3).ToList();
        Assert.NotEmpty(results);
        Assert.Equal(Convert.ToBase64String(probe.Id), Convert.ToBase64String(results[0].entry.Id));
      }

      await reopened.Close();
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public async Task DeletesAndRelinking_NeverGrowTheGraphFiles()
  {
    var root = NewTempRoot();
    Directory.CreateDirectory(root);

    try
    {
      var seeded = await SeedAsync(root, 150, 32, 11, out var store);

      var hnswDir = Path.Combine(root, "hnsw");
      var vecFile = Directory.GetFiles(hnswDir, "*.vec").Single();
      var adjFile = Directory.GetFiles(hnswDir, "*.adj").Single();

      var vecLength = new FileInfo(vecFile).Length;
      var adjLength = new FileInfo(adjFile).Length;

      // Delete half the entries (the entrypoint falls sooner or later) and
      // run searches: everything must happen IN PLACE — the whole point of
      // the split storage is that graph mutations never grow the files.
      for (var i = 0; i < seeded.Count; i += 2)
        Assert.True(await store.DeleteContent("docs", seeded[i].Id));

      for (var i = 1; i < seeded.Count; i += 20)
        Assert.NotEmpty(store.Search("docs", seeded[i].Embedding, 5));

      await store.SaveChangesAsync();

      Assert.Equal(vecLength, new FileInfo(vecFile).Length);
      Assert.Equal(adjLength, new FileInfo(adjFile).Length);

      await store.Close();
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void LegacySingleFileGraph_IsMigrated_OnFirstAccess()
  {
    var root = NewTempRoot();
    var storagePath = Path.Combine(root, "hnsw");
    Directory.CreateDirectory(storagePath);

    try
    {
      var options = new SmallWorldOptions
      {
        M = 16,
        StoragePath = storagePath,
        generator = new Random(7)
      };

      // Hand-build a legacy single-file graph: slot 0 is a full copy of the
      // entrypoint (node 1), nodes 1..3 form a fully connected level 0.
      var legacyPath = Path.Combine(storagePath, "docs.hnsw");
      var vectors = new[]
      {
        new float[] { 1f, 0f, 0f, 0f },
        new float[] { 0f, 1f, 0f, 0f },
        new float[] { 0.7071f, 0.7071f, 0f, 0f }
      };

      var legacy = new StoredList<IndexNode, SmallWorldOptions>(
        new StoreListOptions { FilePath = legacyPath, FlushInterval = TimeSpan.FromMinutes(1) }, options);

      var nodes = new IndexNode[3];
      for (var n = 0; n < 3; n++)
      {
        var connections = Enumerable.Range(1, 3).Where(p => p != n + 1).ToList();
        nodes[n] = new IndexNode(options)
        {
          PositionId = n + 1,
          Id = VectorKey.From(n + 1),
          Vector = vectors[n],
          MaxLevel = 0,
          Connections = new IList<int>[] { connections }
        };
      }

      var slot0 = new IndexNode(options)
      {
        PositionId = 1, // entrypoint pointer, legacy style: full copy of node 1
        Id = nodes[0].Id,
        Vector = nodes[0].Vector,
        MaxLevel = 0,
        Connections = nodes[0].Connections
      };

      legacy.Add(slot0);
      foreach (var node in nodes) legacy.Add(node);
      legacy.DisposeAsync().GetAwaiter().GetResult();

      // First access migrates: legacy file gone, split files in place, and
      // the graph answers queries with the migrated data.
      var indexer = new SmallWorldIndexer(options);
      var query = new IndexNode(options)
      {
        Id = new VectorKey { Value = [] },
        MaxLevel = 0,
        Connections = Array.Empty<IList<int>>(),
        Vector = [1f, 0f, 0f, 0f]
      };

      // KNNSearch returns heap order: sort by distance for ranking asserts.
      var results = indexer.KNNSearch("docs", query, 2).OrderBy(r => r.Distance).ToList();

      Assert.Equal(2, results.Count);
      Assert.Equal(1, results[0].Item.PositionId);                  // exact match: node 1
      Assert.Equal(VectorKey.From(1), results[0].Item.Id);
      Assert.Equal(VectorKey.From(3), results[1].Item.Id);          // 45°: node 3 before node 2

      Assert.False(File.Exists(legacyPath));
      Assert.False(File.Exists($"{legacyPath}.index"));
      Assert.True(File.Exists($"{legacyPath}.vec"));
      Assert.True(File.Exists($"{legacyPath}.adj"));

      indexer.CloseAsync().GetAwaiter().GetResult();
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static async Task<List<(byte[] Id, float[] Embedding)>> SeedAsync(
    string root, int totalVectors, int dimensions, int seed)
  {
    var seeded = await SeedAsync(root, totalVectors, dimensions, seed, out var store);
    await store.Close();
    return seeded;
  }

  private static Task<List<(byte[] Id, float[] Embedding)>> SeedAsync(
    string root, int totalVectors, int dimensions, int seed, out Store store)
  {
    store = new Store(OptionsFor(root));
    return SeedCoreAsync(store, totalVectors, dimensions, seed);
  }

  private static async Task<List<(byte[] Id, float[] Embedding)>> SeedCoreAsync(
    Store store, int totalVectors, int dimensions, int seed)
  {
    var random = new Random(seed);
    var seeded = new List<(byte[] Id, float[] Embedding)>(totalVectors);

    for (var i = 0; i < totalVectors; i++)
    {
      var id = Guid.NewGuid().ToByteArray();
      var embedding = CreateUnitVector(random, dimensions);

      await store.AppendContent(new VectorEntry
      {
        Id = id,
        CollectionName = "docs",
        Content = MessagePackDocumentSerializer.Instance.Serialize($"doc-{i}"),
        Embedding = embedding
      });

      seeded.Add((id, embedding));
    }

    await store.SaveChangesAsync();
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

    if (norm <= 0f) return vector;

    var invNorm = 1f / MathF.Sqrt(norm);
    for (var i = 0; i < dimensions; i++)
      vector[i] *= invNorm;

    return vector;
  }
}
