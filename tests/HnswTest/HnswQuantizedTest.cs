using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;

namespace HnswTest;

/// <summary>
/// SQ8 graph quantization against the known-distance dataset: with the exact
/// rerank enabled (default) the final scores come from the full-precision
/// store embeddings, so rankings must stay coherent with the analytic truth.
/// </summary>
public class HnswQuantizedTest : IAsyncDisposable
{
  private const int Groups = 6;
  private const int PerGroup = 30;
  private const int Dimensions = 32;
  private const double StepRadians = 3.0 * Math.PI / 180.0;
  private const int Top = 10;

  private readonly string _dbRoot;
  private readonly string _collectionName = "sentenze";
  private Store _store;

  private sealed record Sentence(byte[] Id, int Group, double Angle, float[] Embedding);

  public HnswQuantizedTest()
  {
    _dbRoot = Path.Combine(Path.GetTempPath(), "jigen-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dbRoot);
    _store = new Store(OptionsFor());
  }

  private StoreOptions OptionsFor() => new()
  {
    DataBasePath = _dbRoot,
    DataBaseName = "known-distances-sq8",
    Indexer = new SmallWorldIndexer(new SmallWorldOptions
    {
      M = 16,
      EfConstruction = 200,
      EfSearch = 80,
      StoragePath = Path.Combine(_dbRoot, "hnsw"),
      generator = new Random(1234),
      Quantization = VectorQuantization.SQ8
    })
  };

  [Fact]
  public async Task Sq8Search_IsConsistent_WithKnownDistances()
  {
    var dataset = await SeedAsync();
    AssertSearchConsistency(dataset);
  }

  [Fact]
  public async Task Sq8Graph_SurvivesReopen()
  {
    var dataset = await SeedAsync();

    // Reopen: the graph reloads v2 (SQ8) records and traverses them from the
    // memory mapping.
    await _store.Close();
    _store = new Store(OptionsFor());

    AssertSearchConsistency(dataset);
  }

  private void AssertSearchConsistency(List<Sentence> dataset)
  {
    for (var group = 0; group < Groups; group++)
    {
      var phi = 10 * StepRadians + 1.0 * Math.PI / 180.0;
      var query = new float[Dimensions];
      query[2 * group] = (float)Math.Cos(phi);
      query[2 * group + 1] = (float)Math.Sin(phi);

      var expected = dataset
        .Select(s => (Sentence: s, Similarity: s.Group == group ? Math.Cos(s.Angle - phi) : 0.0))
        .OrderByDescending(e => e.Similarity)
        .ToList();

      var groupById = dataset.ToDictionary(s => Convert.ToBase64String(s.Id), s => s.Group, StringComparer.Ordinal);
      var similarityById = expected.ToDictionary(e => Convert.ToBase64String(e.Sentence.Id), e => e.Similarity, StringComparer.Ordinal);

      var results = _store.Search(_collectionName, query, Top).ToList();

      Assert.NotEmpty(results);
      Assert.Equal(
        Convert.ToBase64String(expected[0].Sentence.Id),
        Convert.ToBase64String(results[0].entry.Id));

      foreach (var (entry, score) in results)
      {
        var id = Convert.ToBase64String(entry.Id);
        Assert.Equal(group, groupById[id]);

        // The exact rerank rescores with the full-precision embeddings: the
        // reported score must match the analytic similarity, not the SQ8 one.
        Assert.True(Math.Abs(score - similarityById[id]) < 1e-3,
          $"group {group}: id {id} score {score} != known {similarityById[id]:0.000000}");
      }

      var expectedTop = expected.Take(Top).Select(e => Convert.ToBase64String(e.Sentence.Id)).ToHashSet(StringComparer.Ordinal);
      var got = results.Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
      var recall = (double)got.Intersect(expectedTop, StringComparer.Ordinal).Count() / Top;
      Assert.True(recall >= 0.9, $"group {group}: recall {recall:0.00} below 0.9");
    }
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

        var id = Guid.NewGuid().ToByteArray();
        await _store.AppendContent(new VectorEntry
        {
          Id = id,
          CollectionName = _collectionName,
          Content = MessagePackDocumentSerializer.Instance.Serialize($"sq8-{group:00}-{j:00}"),
          Embedding = embedding
        });

        dataset.Add(new Sentence(id, group, angle, embedding));
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
