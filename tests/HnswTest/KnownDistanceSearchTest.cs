using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Indexers;
using Xunit.Abstractions;

namespace HnswTest;

/// <summary>
/// Reference dataset with analytically known distances.
///
/// 6 groups × 30 sentences = 180 vectors. Each group g lives on its own 2D
/// plane (dimensions 2g and 2g+1): sentence j has embedding
/// v = cos(θj)·e(2g) + sin(θj)·e(2g+1) with θj = j·3°.
/// Cosine similarity is exact by construction:
///   same group   → cos(θi − θj)
///   cross group  → 0 (orthogonal planes)
/// so the full expected ranking for any query is computable in closed form.
///
/// The database is always WRITTEN with the HNSW indexer; the two tests search
/// it with and without the index (brute force) and check both rankings and
/// scores against the known similarities.
/// </summary>
public class KnownDistanceSearchTest : IAsyncDisposable
{
  private const int Groups = 6;
  private const int PerGroup = 30;
  private const int Dimensions = 32;
  private const double StepRadians = 3.0 * Math.PI / 180.0;
  private const int Top = 10;

  private readonly string _dbRoot;
  private readonly string _collectionName = "sentenze";
  private readonly Store _store;

  private sealed record Sentence(byte[] Id, int Group, double Angle, float[] Embedding, string Text);

  private ITestOutputHelper testOutputHelper;
  
  public KnownDistanceSearchTest(ITestOutputHelper testOutputHelper)
  {
    this.testOutputHelper = testOutputHelper;
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);

    testOutputHelper.WriteLine($"Database root: {_dbRoot}");
    
    _store = new Store(new StoreOptions
    {
      DataBasePath = _dbRoot,
      DataBaseName = "known-distances",
      Indexer = new SmallWorldIndexer(new SmallWorldOptions
      {
        M = 16,
        EfConstruction = 200,
        EfSearch = 80,
        StoragePath = Path.Combine(_dbRoot, "hnsw"),
        // Deterministic level assignment: the test must be reproducible.
        generator = new Random(1234)
      })
    });
  }

  [Fact]
  public async Task BruteForceSearch_MatchesKnownDistances_Exactly()
  {
    var dataset = await SeedAsync();
    Assert.True(dataset.Count >= 150);

    var bruteForce = new BruteForceIndexer();

    for (var group = 0; group < Groups; group++)
    {
      var (query, expected) = BuildQuery(dataset, group, referenceIndex: 10);

      var results = bruteForce.Search(_store, _collectionName, query, Top).ToList();

      Assert.Equal(Top, results.Count);

      // Exact search: the ranking must match the analytic one, id by id.
      for (var i = 0; i < Top; i++)
      {
        Assert.Equal(
          Convert.ToBase64String(expected[i].Sentence.Id),
          Convert.ToBase64String(results[i].entry.Id));

        // The score is the dot product of unit vectors: it must equal the
        // known cosine similarity up to float accumulation error.
        Assert.True(Math.Abs(results[i].score - expected[i].Similarity) < 1e-4,
          $"group {group}, rank {i}: score {results[i].score} != expected {expected[i].Similarity:0.000000}");
      }
    }
  }

  [Fact]
  public async Task HnswSearch_IsConsistent_WithKnownDistances()
  {
    var dataset = await SeedAsync();
    Assert.True(dataset.Count >= 150);

    for (var group = 0; group < Groups; group++)
    {
      var (query, expected) = BuildQuery(dataset, group, referenceIndex: 10);
      var expectedTopIds = expected.Take(Top).Select(e => Convert.ToBase64String(e.Sentence.Id)).ToHashSet(StringComparer.Ordinal);
      var similarityById = expected.ToDictionary(e => Convert.ToBase64String(e.Sentence.Id), e => e.Similarity, StringComparer.Ordinal);
      var groupById = dataset.ToDictionary(s => Convert.ToBase64String(s.Id), s => s.Group, StringComparer.Ordinal);

      var results = _store.Search(_collectionName, query, Top).ToList();

      Assert.NotEmpty(results);

      // The best match is unambiguous by construction: it must be exact.
      Assert.Equal(
        Convert.ToBase64String(expected[0].Sentence.Id),
        Convert.ToBase64String(results[0].entry.Id));

      var previousSimilarity = double.MaxValue;
      foreach (var (entry, score) in results)
      {
        var id = Convert.ToBase64String(entry.Id);

        // Every result must come from the query's group: any cross-group
        // vector has similarity exactly 0 and cannot beat 30 in-group ones.
        Assert.Equal(group, groupById[id]);

        // Reported score must match the known similarity of that sentence.
        var known = similarityById[id];
        Assert.True(Math.Abs(score - known) < 1e-4,
          $"group {group}: id {id} score {score} != known {known:0.000000}");

        // Ranking must be consistent with the known distances (non-increasing).
        Assert.True(known <= previousSimilarity + 1e-6,
          $"group {group}: id {id} (sim {known:0.000000}) ranked after a farther result (sim {previousSimilarity:0.000000})");
        previousSimilarity = known;
      }

      // HNSW is approximate, but on 180 well-separated vectors with
      // efSearch 80 it must recover almost all of the true top-10.
      var returnedIds = results.Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
      var recall = (double)returnedIds.Intersect(expectedTopIds, StringComparer.Ordinal).Count() / Top;
      Assert.True(recall >= 0.9, $"group {group}: recall {recall:0.00} below 0.9");
    }
  }

  /// <summary>
  /// Query anchored 1° past sentence <paramref name="referenceIndex"/> of the
  /// group: every |θj − φ| is distinct, so the expected ranking has no ties.
  /// Returns the query vector and ALL sentences ordered by known similarity.
  /// </summary>
  private static (float[] Query, List<(Sentence Sentence, double Similarity)> Expected) BuildQuery(
    List<Sentence> dataset, int group, int referenceIndex)
  {
    var phi = referenceIndex * StepRadians + 1.0 * Math.PI / 180.0;

    var query = new float[Dimensions];
    query[2 * group] = (float)Math.Cos(phi);
    query[2 * group + 1] = (float)Math.Sin(phi);

    var expected = dataset
      .Select(s => (Sentence: s, Similarity: s.Group == group ? Math.Cos(s.Angle - phi) : 0.0))
      .OrderByDescending(e => e.Similarity)
      .ToList();

    return (query, expected);
  }

  private async Task<List<Sentence>> SeedAsync()
  {
    var dataset = new List<Sentence>(Groups * PerGroup);

    for (var group = 0; group < Groups; group++)
    {
      for (var j = 0; j < PerGroup; j++)
      {
        var angle = j * StepRadians;
        var embedding = new float[Dimensions];
        embedding[2 * group] = (float)Math.Cos(angle);
        embedding[2 * group + 1] = (float)Math.Sin(angle);

        var text = $"Sentenza {group:00}-{j:00}: la sezione {group} tratta la materia con orientamento di grado {j * 3}.";
        var id = Guid.NewGuid().ToByteArray();

        await _store.AppendContent(new VectorEntry
        {
          Id = id,
          CollectionName = _collectionName,
          Content = MessagePackDocumentSerializer.Instance.Serialize(text),
          Embedding = embedding
        });

        dataset.Add(new Sentence(id, group, angle, embedding, text));
      }
    }

    await _store.SaveChangesAsync();
    return dataset;
  }

  public async ValueTask DisposeAsync()
  {
    await _store.Close();
    _store.Dispose();

    if (Directory.Exists(_dbRoot))
      Directory.Delete(_dbRoot, recursive: true);
  }
}
