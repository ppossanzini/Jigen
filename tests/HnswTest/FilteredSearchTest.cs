using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;
using Jigen.Indexer;
using Jigen.Indexers;
using Xunit.Abstractions;

namespace HnswTest;

/// <summary>
/// 240 vectors on a single circle, query anchored at angle 0. Cosine
/// similarity falls off monotonically with angular distance, so the naive
/// nearest-ef window (ef = 64) covers only a ~96° arc around the query.
/// Only a 10-vector arc near the OPPOSITE point of the circle (angle ~180°,
/// cosine similarity ~ -1) is tagged "target": those vectors are the
/// farthest possible from the query and are guaranteed to fall outside that
/// window.
///
/// Post-filtering a fixed unfiltered window (the pre-fix behaviour) would
/// return zero results here even though 10 matching vectors exist in the
/// graph. The ACORN-1-style search evaluates the filter during traversal,
/// so it keeps expanding until it finds them.
/// </summary>
public sealed class FilteredSearchTest : IAsyncDisposable
{
  private const int TotalVectors = 240;
  private const int Dimensions = 2;
  private const double StepRadians = 2.0 * Math.PI / TotalVectors; // 1.5° per step
  private const int TargetStart = 118; // ~177° from the query
  private const int TargetCount = 10;

  private readonly string _dbRoot;
  private readonly string _collectionName = "circle";
  private Store _store;

  public sealed class Doc
  {
    public string Tag { get; set; }
  }

  public FilteredSearchTest(ITestOutputHelper testOutputHelper)
  {
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null)
      throw new DirectoryNotFoundException("The repository root could not be located.");

    _dbRoot = Path.Combine(root.FullName, ".temp", "shared-filter-tests", Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_dbRoot);
    testOutputHelper.WriteLine($"Database root: {_dbRoot}");

    _store = OpenStore();
  }

  private Store OpenStore(bool bruteForce = false) => new(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "filtered-search",
      Indexer = bruteForce ? new BruteForceIndexer() : new SmallWorldIndexer(new SmallWorldOptions
      {
        M = 16,
        EfConstruction = 200,
        EfSearch = 64,
        StoragePath = Path.Combine(_dbRoot, "hnsw"),
        // Deterministic level assignment: the test must be reproducible.
        generator = new Random(4242)
      })
    });

  [Fact]
  public async Task SharedFilter_ObservesWriterUpdate_AfterRebuildingDamagedIndexes()
  {
    for (var number = 1; number <= 3; number++)
      await _store.AppendContent(new VectorEntry
      {
        Id = BitConverter.GetBytes(number),
        CollectionName = _collectionName,
        Content = MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "other" }),
        Embedding = new float[] { 1f, number * 0.1f }
      });
    await _store.SaveChangesAsync();

    var metadata = _store.SharedMetadata.AddMetadata(
      MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "target" }).ToArray());
    _store.SharedMetadata.AssociateEmbedding(_collectionName, BitConverter.GetBytes(1), metadata);
    _store.SharedMetadata.AssociateEmbedding(_collectionName, BitConverter.GetBytes(2), metadata);
    AssertFilteredIds("target", 1, 2);

    await _store.AppendContent(new VectorEntry
    {
      Id = BitConverter.GetBytes(1),
      CollectionName = _collectionName,
      Content = MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "updated" }),
      Embedding = new float[] { 1f, 0.1f }
    });
    await _store.SaveChangesAsync();
    AssertFilteredIds("target");
    AssertFilteredIds("updated", 1, 2);
    AssertFilteredIds("other", 3);

    await _store.Close();
    _store.Dispose();
    File.WriteAllBytes(Path.Combine(_dbRoot, "filtered-search.shared.associations.jigen.lookup"), [1, 2, 3]);
    File.WriteAllBytes(Path.Combine(_dbRoot, "filtered-search.shared.contents.jigen.versions"), [4, 5, 6]);
    _store = OpenStore();

    AssertFilteredIds("target");
    AssertFilteredIds("updated", 1, 2);
    AssertFilteredIds("other", 3);
    Assert.Null(_store.GetContent(_collectionName, BitConverter.GetBytes(99)));
  }

  [Fact]
  public async Task SharedFilter_DeleteReassociateAndClear_PreserveOtherCollectionAfterReopen()
  {
    byte[] firstId = BitConverter.GetBytes(1);
    byte[] secondId = BitConverter.GetBytes(2);
    var content = MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "target" });
    await _store.AppendContentBulk([
      new VectorEntry { Id = firstId, CollectionName = _collectionName, Content = content, Embedding = new float[] { 1f, 0f } },
      new VectorEntry { Id = secondId, CollectionName = _collectionName, Content = content, Embedding = new float[] { 1f, 0.1f } },
      new VectorEntry { Id = firstId, CollectionName = "other", Content = content, Embedding = new float[] { 1f, 0f } }
    ]);
    await _store.SaveChangesAsync();
    var metadata = _store.SharedMetadata.AddMetadata(content.ToArray());
    _store.SharedMetadata.AssociateEmbedding(_collectionName, firstId, metadata);
    _store.SharedMetadata.AssociateEmbedding(_collectionName, secondId, metadata);
    _store.SharedMetadata.AssociateEmbedding("other", firstId, metadata);

    Assert.True(await _store.DeleteContent(_collectionName, firstId));
    Assert.False(await _store.DeleteContent(_collectionName, firstId));
    await _store.SaveChangesAsync();
    AssertFilteredIds("target", 2);
    Assert.Null(_store.GetContent(_collectionName, firstId));
    Assert.Null(_store.SharedMetadata.GetContent(_collectionName, firstId));
    Assert.Equal(content.ToArray(), _store.GetContent("other", firstId));

    await _store.AppendContent(new VectorEntry
    {
      Id = firstId, CollectionName = _collectionName, Content = content,
      Embedding = new float[] { 1f, 0f }
    });
    await _store.SaveChangesAsync();
    _store.SharedMetadata.AssociateEmbedding(_collectionName, firstId, metadata);
    AssertFilteredIds("target", 1, 2);
    Assert.Equal(2, await _store.ClearContent(_collectionName));
    await _store.SaveChangesAsync();
    AssertFilteredIds("target");

    await _store.Close();
    _store.Dispose();
    _store = OpenStore();
    AssertFilteredIds("target");
    Assert.Null(_store.GetContent(_collectionName, firstId));
    Assert.Null(_store.SharedMetadata.GetContent(_collectionName, firstId));
    Assert.Null(_store.SharedMetadata.GetContent(_collectionName, secondId));
    Assert.Equal(content.ToArray(), _store.GetContent("other", firstId));
    var surviving = _store.Search("other", [1f, 0f], top: 10,
      contentFilter: new PropertyEqualsFilter { PropertyPath = "Tag", Value = "target" }).ToList();
    Assert.Single(surviving);
    Assert.Equal(firstId, surviving[0].Item1.Id);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EmptyContent_IsPresentAfterReopen_ForSingleAndBulkWrites(bool bulk)
  {
    var entry = new VectorEntry
    {
      Id = BitConverter.GetBytes(1), CollectionName = _collectionName,
      Content = Array.Empty<byte>(), Embedding = new float[] { 1f, 0f }
    };
    if (bulk)
      await _store.AppendContentBulk([entry]);
    else
      await _store.AppendContent(entry);
    await _store.SaveChangesAsync();
    Assert.True(_store.TryGetContent(_collectionName, entry.Id, out var content));
    Assert.Empty(content);

    await _store.Close();
    _store.Dispose();
    _store = OpenStore();
    Assert.True(_store.TryGetContent(_collectionName, entry.Id, out content));
    Assert.Empty(content);
    Assert.False(_store.TryGetContent(_collectionName, BitConverter.GetBytes(99), out var absent));
    Assert.Null(absent);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GroupedSearch_MatchesBruteForce_WithUnbalancedGroupsAndExplicitBudget(bool bruteForce)
  {
    await _store.Close();
    _store.Dispose();
    _store = OpenStore(bruteForce);

    var content = MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "target" });
    for (var number = 0; number < 8; number++)
    {
      var angle = number < 6 ? 0.05 * (number + 1) : 0.7 + (number - 6) * 0.4;
      await _store.AppendContent(new VectorEntry
      {
        Id = BitConverter.GetBytes(number), CollectionName = _collectionName, Content = content,
        Embedding = new float[] { (float)Math.Cos(angle), (float)Math.Sin(angle) }
      });
    }
    await _store.SaveChangesAsync();

    var metadataIds = Enumerable.Range(0, 3)
      .Select(_ => _store.SharedMetadata.AddMetadata(content.ToArray())).ToArray();
    var metadataByEmbedding = new Dictionary<int, int>();
    for (var number = 0; number < 8; number++)
    {
      var metadata = metadataIds[number < 6 ? 0 : number - 5];
      metadataByEmbedding[number] = metadata;
      _store.SharedMetadata.AssociateEmbedding(_collectionName, BitConverter.GetBytes(number), metadata);
    }

    float[] query = [1f, 0f];
    var expected = new BruteForceIndexer().Search(_store, _collectionName, query, 8)
      .GroupBy(candidate => metadataByEmbedding[BitConverter.ToInt32(candidate.entry.Id)])
      .Select(group => (metadataIndex: group.Key, winner: group.MaxBy(candidate => candidate.score)))
      .OrderByDescending(group => group.winner.score).ToArray();

    var results = _store.SearchByMetadata(_collectionName, query, top: 3, candidateBudget: 64).ToArray();
    Assert.Equal(expected.Select(group => group.metadataIndex), results.Select(group => group.metadataIndex));
    for (var position = 0; position < expected.Length; position++)
    {
      Assert.Equal(expected[position].winner.entry.Id, results[position].entry.Id);
      Assert.Equal(expected[position].winner.score, results[position].score, precision: 5);
      Assert.Equal(content.ToArray(), results[position].entry.Content.ToArray());
    }

    var limited = _store.SearchByMetadata(_collectionName, query, top: 3, candidateBudget: 9).ToArray();
    var onlyGroup = Assert.Single(limited);
    Assert.Equal(metadataIds[0], onlyGroup.metadataIndex);
    Assert.Equal(BitConverter.GetBytes(0), onlyGroup.entry.Id);
    Assert.Equal(expected[0].winner.score, onlyGroup.score, precision: 5);

    Assert.True(_store.SharedMetadata.UpdateMetadata(metadataIds[0],
      MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = "excluded" }).ToArray()));
    var filtered = _store.SearchByMetadata(_collectionName, query, top: 3, candidateBudget: 64,
      contentFilter: new PropertyEqualsFilter { PropertyPath = "Tag", Value = "target" }).ToArray();
    Assert.Equal(metadataIds.Skip(1), filtered.Select(group => group.metadataIndex));
    Assert.All(filtered, group => Assert.Equal("target",
      MessagePackDocumentSerializer.Instance.Deserialize<Doc>(group.entry.Content).Tag));
    Assert.Empty(_store.SearchByMetadata(_collectionName, query, top: 3, candidateBudget: 64,
      contentFilter: new PropertyEqualsFilter { PropertyPath = "Tag", Value = "absent" }));
    Assert.Empty(_store.SearchByMetadata("missing", query, top: 3, candidateBudget: 64));

    await _store.Close();
    _store.Dispose();
    _store = OpenStore(bruteForce);
    var reopened = _store.SearchByMetadata(_collectionName, query, top: 3, candidateBudget: 64,
      contentFilter: new PropertyEqualsFilter { PropertyPath = "Tag", Value = "target" }).ToArray();
    Assert.Equal(filtered.Select(group => group.metadataIndex), reopened.Select(group => group.metadataIndex));
    Assert.Equal(filtered.Select(group => group.score), reopened.Select(group => group.score));
  }

  [Theory]
  [InlineData(0, 10)]
  [InlineData(3, 0)]
  [InlineData(-1, 10)]
  [InlineData(3, -1)]
  public void GroupedSearch_NonpositiveTopOrBudget_ReturnsEmpty(int top, int budget)
  {
    Assert.Empty(_store.SearchByMetadata(_collectionName, [1f, 0f], top, budget));
  }

  private void AssertFilteredIds(string tag, params int[] expected)
  {
    var results = _store.Search(_collectionName, [1f, 0f], top: 10,
      contentFilter: new PropertyEqualsFilter { PropertyPath = "Tag", Value = tag }).ToList();
    Assert.Equal(expected.Order(), results.Select(result => BitConverter.ToInt32(result.Item1.Id)).Order());
    foreach (var result in results)
      Assert.Equal(tag, MessagePackDocumentSerializer.Instance.Deserialize<Doc>(result.Item1.Content).Tag);
  }

  [Fact]
  public async Task Search_WithSelectiveFilter_FindsMatchesOutsideTheNearestEfWindow()
  {
    for (var j = 0; j < TotalVectors; j++)
    {
      var angle = j * StepRadians;
      var embedding = new float[Dimensions]
      {
        (float)Math.Cos(angle),
        (float)Math.Sin(angle)
      };

      var isTarget = j >= TargetStart && j < TargetStart + TargetCount;

      await _store.AppendContent(new VectorEntry
      {
        Id = BitConverter.GetBytes(j),
        CollectionName = _collectionName,
        Content = MessagePackDocumentSerializer.Instance.Serialize(new Doc { Tag = isTarget ? "target" : "other" }),
        Embedding = embedding
      });
    }

    await _store.SaveChangesAsync();

    var query = new float[Dimensions] { 1f, 0f }; // angle 0
    IFilterExpression filter = new PropertyEqualsFilter { PropertyPath = "Tag", Value = "target" };

    var results = _store.Search(_collectionName, query, top: TargetCount, contentFilter: filter).ToList();

    Assert.Equal(TargetCount, results.Count);

    foreach (var (entry, _) in results)
    {
      var doc = MessagePackDocumentSerializer.Instance.Deserialize<Doc>(entry.Content);
      Assert.Equal("target", doc.Tag);

      var j = BitConverter.ToInt32(entry.Id);
      Assert.InRange(j, TargetStart, TargetStart + TargetCount - 1);
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _store.Close();
    _store.Dispose();

    if (Directory.Exists(_dbRoot))
      Directory.Delete(_dbRoot, recursive: true);
  }
}
