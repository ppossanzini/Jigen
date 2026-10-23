using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Indexers;
using Jigen.Extensions;

namespace JigenTests;

public class MultiVectorTests
{
  private static StoreOptions OptionsFor(string path, bool wal = false) => new()
  {
    DataBaseName = "multivectortest",
    DataBasePath = path,
    Indexer = new BruteForceIndexer(),
    Wal = wal ? new WalOptions { Enabled = true } : null
  };

  private static VectorEntry Entry(byte[] id, params float[][] vectors) => new()
  {
    Id = id,
    CollectionName = "docs",
    Content = Encoding.UTF8.GetBytes("content-" + Convert.ToHexString(id)),
    Embeddings = vectors.Select(v => (ReadOnlyMemory<float>)v).ToArray()
  };

  private static string NewPath()
  {
    var path = Path.Combine(Path.GetTempPath(), $"jigen-multivector-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
  }

  [Fact]
  public async Task MultiVectorEntry_RoundTripsEveryVector()
  {
    var path = NewPath();
    try
    {
      var store = new Store(OptionsFor(path));
      var id = Guid.NewGuid().ToByteArray();

      await store.AppendContent(Entry(id, [1f, 0f, 0f], [0f, 1f, 0f], [0f, 0f, 1f]));
      await store.SaveChangesAsync();

      var vectors = store.GetEmbeddings("docs", id);
      Assert.NotNull(vectors);
      Assert.Equal(3, vectors.Length);
      Assert.Equal([1f, 0f, 0f], vectors[0]);
      Assert.Equal([0f, 1f, 0f], vectors[1]);
      Assert.Equal([0f, 0f, 1f], vectors[2]);

      // The single-vector accessor keeps returning the first vector.
      Assert.Equal([1f, 0f, 0f], store.GetEmbedding("docs", id));

      await store.Close();
    }
    finally
    {
      Directory.Delete(path, true);
    }
  }

  [Fact]
  public async Task MultiVectorEntry_SurvivesReopen()
  {
    var path = NewPath();
    try
    {
      var id = Guid.NewGuid().ToByteArray();
      var store = new Store(OptionsFor(path));
      await store.AppendContent(Entry(id, [1f, 2f], [3f, 4f]));
      await store.SaveChangesAsync();
      await store.Close();

      var reopened = new Store(OptionsFor(path));
      var vectors = reopened.GetEmbeddings("docs", id);
      Assert.NotNull(vectors);
      Assert.Equal(2, vectors.Length);
      Assert.Equal([3f, 4f], vectors[1]);
      await reopened.Close();
    }
    finally
    {
      Directory.Delete(path, true);
    }
  }

  [Fact]
  public async Task WalReplay_RestoresEveryVector()
  {
    var path = NewPath();
    try
    {
      // Create the database files, then leave a single insert record in the
      // WAL: what an unclean shutdown between the WAL append and the writer
      // batch leaves behind.
      await (new Store(OptionsFor(path))).Close();

      var id = Guid.NewGuid().ToByteArray();
      var entry = Entry(id, [1f, 2f], [3f, 4f]);
      var content = entry.Content.ToArray();
      var embeddings = entry.Vectors.Select(v => v.ToArray()).ToArray();
      var buffer = new byte[WalRecord.InsertRecordSize(entry.Id, entry.CollectionName, content, embeddings)];
      WalRecord.SerializeInsert(buffer, entry);
      await File.WriteAllBytesAsync(Path.Combine(path, "multivectortest.wal.jigen"), buffer);

      var store = new Store(OptionsFor(path, wal: true));
      await store.SaveChangesAsync();

      var vectors = store.GetEmbeddings("docs", id);
      Assert.NotNull(vectors);
      Assert.Equal(2, vectors.Length);
      Assert.Equal([1f, 2f], vectors[0]);
      Assert.Equal([3f, 4f], vectors[1]);
      await store.Close();
    }
    finally
    {
      Directory.Delete(path, true);
    }
  }

  [Fact]
  public async Task MultiVectorEntry_ScoresAsItsClosestVector()
  {
    var path = NewPath();
    try
    {
      var store = new Store(OptionsFor(path));
      var id = Guid.NewGuid().ToByteArray();

      await store.AppendContent(Entry(id, [1f, 0f], [0f, 1f]));
      await store.SaveChangesAsync();

      var query = new[] { 0f, 1f };
      var result = store.Search("docs", query, 1).First();
      Assert.Equal(id, result.entry.Id);
      Assert.Equal(1f, result.score, 5);
    }
    finally
    {
      if (Directory.Exists(path)) Directory.Delete(path, true);
    }
  }

  [Fact]
  public async Task HnswCollection_FindsDocumentThroughAnyOfItsVectors()
  {
    var path = NewPath();
    try
    {
      var store = new Store(new StoreOptions
      {
        DataBaseName = "multivectortest",
        DataBasePath = path,
        Indexer = new Jigen.Indexer.SmallWorldIndexer(new Jigen.Indexer.SmallWorldOptions
        {
          M = 16,
          EfConstruction = 200,
          EfSearch = 80,
          StoragePath = Path.Combine(path, "hnsw")
        })
      });

      var id = Guid.NewGuid().ToByteArray();
      float[] v0 = [1f, 0f, 0f], v1 = [0f, 1f, 0f], v2 = [0f, 0f, 1f];
      await store.AppendContent(Entry(id, v0, v1, v2));
      await store.SaveChangesAsync();

      foreach (var query in new[] { v0, v1, v2 })
      {
        var hit = store.Search("docs", query, 1).First();
        Assert.Equal(id, hit.entry.Id);
        Assert.Equal(1f, hit.score, 4);
      }

      await store.Close();
    }
    finally
    {
      Directory.Delete(path, true);
    }
  }

  [Fact]
  public async Task MixedVectorCountsAreRejected()
  {
    var path = NewPath();
    try
    {
      var store = new Store(OptionsFor(path));
      await Assert.ThrowsAsync<ArgumentException>(() => store.AppendContent(new VectorEntry
      {
        Id = Guid.NewGuid().ToByteArray(),
        CollectionName = "docs",
        Embeddings = [new float[] { 1f, 2f }, new float[] { 3f }]
      }));
      await store.Close();
    }
    finally
    {
      Directory.Delete(path, true);
    }
  }
}
