using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;
using Jigen.Indexer;
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
  private readonly Store _store;

  public sealed class Doc
  {
    public string Tag { get; set; }
  }

  public FilteredSearchTest(ITestOutputHelper testOutputHelper)
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);
    testOutputHelper.WriteLine($"Database root: {_dbRoot}");

    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "filtered-search",
      Indexer = new SmallWorldIndexer(new SmallWorldOptions
      {
        M = 16,
        EfConstruction = 200,
        EfSearch = 64,
        StoragePath = Path.Combine(_dbRoot, "hnsw"),
        // Deterministic level assignment: the test must be reproducible.
        generator = new Random(4242)
      })
    });
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
