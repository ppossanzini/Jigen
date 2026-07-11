using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;

namespace HnswTest;

public class GraphSnapshotTest : IAsyncDisposable
{
  private readonly string _dbRoot;
  private readonly string _collectionName = "graph";
  private readonly Store _store;
  private readonly IExplorableIndex _explorable;

  public GraphSnapshotTest()
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);

    var indexer = new SmallWorldIndexer(new SmallWorldOptions
    {
      M = 16,
      EfConstruction = 200,
      EfSearch = 80,
      StoragePath = Path.Combine(_dbRoot, "hnsw")
    });
    _explorable = indexer;

    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "hnsw-graph",
      Indexer = indexer
    });
  }

  [Fact]
  public async Task GetGraphSnapshot_RespectsLimit_AndReturnsValidCoordinates()
  {
    await SeedAsync(totalVectors: 300, dimensions: 32, seed: 7);

    var snapshot = _explorable.GetGraphSnapshot(_collectionName, dimensions: 2, limit: 100);

    Assert.Equal(100, snapshot.ReturnedNodes);
    Assert.True(snapshot.Truncated);
    Assert.All(snapshot.Nodes, n =>
    {
      Assert.Equal(2, n.Position.Length);
      Assert.All(n.Position, c => Assert.InRange(c, -1f, 1f));
    });

    var positionIds = snapshot.Nodes.Select(n => n.PositionId).ToHashSet();
    Assert.All(snapshot.Edges, e =>
    {
      Assert.Contains(e.Source, positionIds);
      Assert.Contains(e.Target, positionIds);
    });

    Assert.Contains(snapshot.EntrypointPositionId, positionIds);
  }

  [Fact]
  public async Task GetGraphSnapshot_WithHighLimit_ReturnsEveryLiveNode()
  {
    await SeedAsync(totalVectors: 300, dimensions: 32, seed: 7);

    var snapshot = _explorable.GetGraphSnapshot(_collectionName, dimensions: 3, limit: 1000);

    Assert.Equal(300, snapshot.ReturnedNodes);
    Assert.False(snapshot.Truncated);
    Assert.All(snapshot.Nodes, n => Assert.Equal(3, n.Position.Length));
  }

  [Fact]
  public async Task GetGraphSnapshot_WithLevelFilter_OnlyReturnsThatLayer()
  {
    await SeedAsync(totalVectors: 300, dimensions: 32, seed: 7);

    var snapshot = _explorable.GetGraphSnapshot(_collectionName, dimensions: 2, limit: 1000, level: 1);

    Assert.All(snapshot.Nodes, n => Assert.True(n.MaxLevel >= 1));
    Assert.All(snapshot.Edges, e => Assert.Equal(1, e.Level));
  }

  [Fact]
  public async Task GetIndexInfo_ReportsNodeCountsAndSize()
  {
    await SeedAsync(totalVectors: 300, dimensions: 32, seed: 7);

    var info = _explorable.GetIndexInfo(_collectionName);

    Assert.Equal(300, info.Nodes);
    Assert.Equal(0, info.DeletedNodes);
    Assert.True(info.IndexSizeBytes > 0);
    Assert.Equal(300, info.NodesPerLevel.Sum());
    Assert.True(info.AverageDegree > 0);
  }

  [Fact]
  public async Task GetGraphSnapshot_IsDeterministic()
  {
    await SeedAsync(totalVectors: 150, dimensions: 24, seed: 11);

    var first = _explorable.GetGraphSnapshot(_collectionName, dimensions: 2, limit: 1000);
    var second = _explorable.GetGraphSnapshot(_collectionName, dimensions: 2, limit: 1000);

    var firstByPosition = first.Nodes.ToDictionary(n => n.PositionId, n => n.Position);
    foreach (var node in second.Nodes)
    {
      var expected = firstByPosition[node.PositionId];
      Assert.Equal(expected[0], node.Position[0], precision: 4);
      Assert.Equal(expected[1], node.Position[1], precision: 4);
    }
  }

  private async Task SeedAsync(int totalVectors, int dimensions, int seed)
  {
    var random = new Random(seed);

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
    }

    await _store.SaveChangesAsync();
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
