using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Filtering;
using Jigen.Indexers;

namespace JigenTests;

public class SparseSearchTests
{
  private static string NewPath() =>
    Path.Combine(Path.GetTempPath(), $"jigen-sparse-search-{Guid.NewGuid():N}");

  private static StoreOptions OptionsFor(string path) => new()
  {
    DataBaseName = "search",
    DataBasePath = path,
    Wal = new WalOptions { Enabled = true },
    SparseIndex = new SparseIndex()
  };

  private static SparseVector Vector(IEnumerable<(int term, float weight)> pairs) =>
    SparseVector.FromPairs(pairs.Select(p => new KeyValuePair<int, float>(p.term, p.weight)));

  private static VectorEntry Entry(string key, IEnumerable<(int term, float weight)> terms,
    string collection = "docs", string tag = null) => new()
  {
    Id = Encoding.UTF8.GetBytes(key),
    CollectionName = collection,
    // Content is stored as MessagePack, the format the filter evaluator reads.
    Content = MessagePackDocumentSerializer.Instance.Serialize(
      tag is null ? new { name = key } : new { name = key, tag }),
    Embedding = new float[] { 1f, 0f, 0f },
    Sparse = Vector(terms)
  };

  private static List<(int term, float weight)> RandomTerms(Random random, int count)
  {
    var terms = new SortedSet<int>();
    while (terms.Count < count) terms.Add(random.Next(0, 200));
    return terms.Select(term => (term, MathF.Round((float)random.NextDouble() + 0.01f, 3))).ToList();
  }

  [Fact]
  public async Task SearchSparse_MatchesAnExhaustiveDotProduct()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));

      var random = new Random(1234);
      var corpus = new Dictionary<string, List<(int term, float weight)>>();
      var entries = new List<VectorEntry>();

      for (var i = 0; i < 1000; i++)
      {
        var key = $"doc-{i:D4}";
        var terms = RandomTerms(random, random.Next(2, 8));
        corpus[key] = terms;
        entries.Add(Entry(key, terms));
      }

      await store.AppendContentBulk(entries);
      await store.SaveChangesAsync();

      foreach (var queryTerms in new[]
               {
                 new List<(int, float)> { (10, 1f) },
                 new List<(int, float)> { (10, 1f), (33, 0.5f), (77, 2f), (120, 1f), (199, 0.25f) },
                 Enumerable.Range(0, 20).Select(t => (t, 1f)).ToList()
               })
      {
        var query = Vector(queryTerms);
        var actual = store.SearchSparse("docs", query, 10).ToArray();

        var expected = corpus
          .Select(pair => (key: pair.Key, score: pair.Value
            .ToDictionary(t => t.term, t => t.weight)
            .Where(t => query.TermsSpan.Contains(t.Key))
            .Sum(t => t.Value * query.WeightsSpan[query.TermsSpan.IndexOf(t.Key)])))
          .Where(x => x.score > 0)
          .OrderByDescending(x => x.score)
          .ThenBy(x => x.key, StringComparer.Ordinal)
          .Take(10)
          .ToArray();

        Assert.Equal(expected.Select(x => x.key), actual.Select(x => Encoding.UTF8.GetString(x.key)));
        for (var i = 0; i < expected.Length; i++)
          Assert.Equal(expected[i].score, actual[i].score, 3);
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchSparse_WithTermsAbsentFromTheCollection_ReturnsEmpty()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", [(1, 1f)]));
      await store.SaveChangesAsync();

      var result = store.SearchSparse("docs", Vector([(999, 1f)]), 10);

      Assert.Empty(result);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchSparse_WithFilter_ReturnsOnlyMatchingEntries()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", [(1, 1f)], tag: "keep"));
      await store.AppendContent(Entry("b", [(1, 2f)], tag: "drop"));
      await store.AppendContent(Entry("c", [(1, 3f)], tag: "keep"));
      await store.SaveChangesAsync();

      var filter = new PropertyEqualsFilter { PropertyPath = "tag", Value = "keep" };
      var result = store.SearchSparse("docs", Vector([(1, 1f)]), 10, filter).ToArray();

      Assert.Equal(2, result.Length);
      Assert.Equal(new[] { "c", "a" },
        result.Select(r => Encoding.UTF8.GetString(r.key)).ToArray());
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchSparseByMetadata_ReturnsOneGroupPerSharedRecord()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));

      await store.AppendContent(Entry("a1", [(1, 1f)]));
      await store.AppendContent(Entry("a2", [(1, 3f)]));
      await store.AppendContent(Entry("b1", [(1, 2f)]));
      await store.SaveChangesAsync();

      var sharedA = store.SharedMetadata.AddMetadata(Encoding.UTF8.GetBytes("{\"doc\":\"A\"}"));
      store.SharedMetadata.AssociateEmbedding("docs", Encoding.UTF8.GetBytes("a1"), sharedA);
      store.SharedMetadata.AssociateEmbedding("docs", Encoding.UTF8.GetBytes("a2"), sharedA);

      var sharedB = store.SharedMetadata.AddMetadata(Encoding.UTF8.GetBytes("{\"doc\":\"B\"}"));
      store.SharedMetadata.AssociateEmbedding("docs", Encoding.UTF8.GetBytes("b1"), sharedB);

      var result = store.SearchSparseByMetadata("docs", Vector([(1, 1f)]), 10, 100).ToArray();

      Assert.Equal(2, result.Length);
      Assert.Equal(sharedA, result[0].metadataIndex);
      Assert.Equal("a2", Encoding.UTF8.GetString(result[0].key));
      Assert.Equal(3f, result[0].score);
      Assert.Equal(sharedB, result[1].metadataIndex);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchSparseByMetadata_WithTinyBudget_ReturnsFewerGroupsWithoutError()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      for (var i = 0; i < 10; i++)
        await store.AppendContent(Entry($"doc-{i}", [(1, i + 1f)]));
      await store.SaveChangesAsync();

      var result = store.SearchSparseByMetadata("docs", Vector([(1, 1f)]), 10, 3).ToArray();

      Assert.True(result.Length <= 10);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SparseIndex_IsRebuiltFromTheArchiveWhenFilesAreDeleted()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using (var store = new Store(OptionsFor(path)))
      {
        await store.AppendContent(Entry("a", [(1, 1f)]));
        await store.AppendContent(Entry("b", [(1, 2f)]));
        await store.SaveChangesAsync();
        Assert.Equal(2, store.SearchSparse("docs", Vector([(1, 1f)]), 10).Count());
        await store.Close();
      }

      // A new store has no index in memory: the rebuild comes from the files.
      using (var reopened = new Store(OptionsFor(path)))
      {
        var result = reopened.SearchSparse("docs", Vector([(1, 1f)]), 10).ToArray();
        Assert.Equal(2, result.Length);
        Assert.Equal("b", Encoding.UTF8.GetString(result[0].key));

        var info = reopened.GetCollectionInfo("docs");
        Assert.NotNull(info.Sparse);
        Assert.True(info.Sparse.Built);
        // Both entries carry the same single term.
        Assert.Equal(1, info.Sparse.Terms);
        Assert.Equal(2, info.Sparse.Postings);
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchHybrid_FusesBothModalitiesAndMarksProvenance()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));

      // "a" is the best dense hit, "c" the best lexical one.
      await store.AppendContent(new VectorEntry
      {
        Id = Encoding.UTF8.GetBytes("a"),
        CollectionName = "docs",
        Content = MessagePackDocumentSerializer.Instance.Serialize(new { name = "a" }),
        Embedding = new float[] { 1f, 0f, 0f },
        Sparse = Vector([(1, 1f)])
      });
      await store.AppendContent(new VectorEntry
      {
        Id = Encoding.UTF8.GetBytes("b"),
        CollectionName = "docs",
        Content = MessagePackDocumentSerializer.Instance.Serialize(new { name = "b" }),
        Embedding = new float[] { 0.5f, 0.5f, 0f },
        Sparse = Vector([(1, 2f)])
      });
      await store.AppendContent(new VectorEntry
      {
        Id = Encoding.UTF8.GetBytes("c"),
        CollectionName = "docs",
        Content = MessagePackDocumentSerializer.Instance.Serialize(new { name = "c" }),
        Embedding = new float[] { 0f, 1f, 0f },
        Sparse = Vector([(1, 5f)])
      });
      await store.SaveChangesAsync();

      var hybrid = store.SearchHybrid("docs", new float[] { 1f, 0f, 0f }, Vector([(1, 1f)]), 3).ToArray();

      Assert.Equal(3, hybrid.Length);
      Assert.All(hybrid, r => Assert.True(r.inDense || r.inSparse));
      Assert.Contains(hybrid, r => Encoding.UTF8.GetString(r.entry.Id) == "c" && r.inSparse);
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchHybrid_WithOneWeightZero_MatchesTheOtherModality()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", [(1, 1f)]));
      await store.AppendContent(Entry("b", [(1, 4f)]));
      await store.SaveChangesAsync();

      var lexicalOnly = store.SearchHybrid("docs", new float[] { 1f, 0f, 0f }, Vector([(1, 1f)]), 2,
        denseWeight: 0f, sparseWeight: 1f).ToArray();

      Assert.Equal(new[] { "b", "a" },
        lexicalOnly.Select(r => Encoding.UTF8.GetString(r.entry.Id)).ToArray());
      Assert.All(lexicalOnly, r => Assert.False(r.inDense));
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SearchHybrid_WithBothWeightsZero_IsRejected()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", [(1, 1f)]));
      await store.SaveChangesAsync();

      Assert.Throws<ArgumentException>(() =>
        store.SearchHybrid("docs", new float[] { 1f, 0f, 0f }, Vector([(1, 1f)]), 2,
          denseWeight: 0f, sparseWeight: 0f).ToArray());
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }

  [Fact]
  public async Task SparseIndex_RemovingAnEntry_DropsItsPostings()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      using var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry("a", [(1, 1f)]));
      await store.AppendContent(Entry("b", [(1, 2f), (5, 1f)]));
      await store.SaveChangesAsync();

      Assert.Equal(2, store.SearchSparse("docs", Vector([(1, 1f)]), 10).Count());

      await store.DeleteContent("docs", Encoding.UTF8.GetBytes("a"));
      await store.SaveChangesAsync();

      var result = store.SearchSparse("docs", Vector([(1, 1f)]), 10).ToArray();
      Assert.Single(result);
      Assert.Equal("b", Encoding.UTF8.GetString(result[0].key));
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
