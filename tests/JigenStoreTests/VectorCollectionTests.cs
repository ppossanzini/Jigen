using Jigen;
using Jigen.DataStructures;

namespace JigenTests;

public class VectorCollectionTests
{
  public sealed class Article
  {
    public string Title { get; set; }
    public string Category { get; set; }
  }

  private static float[] UnitVector(int dimensions, int direction)
  {
    var vector = new float[dimensions];
    vector[direction % dimensions] = 1f;
    return vector;
  }

  private static async Task WithStore(Func<Store, Task> test)
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-veccoll-{Guid.NewGuid():N}");
    Directory.CreateDirectory(basePath);

    try
    {
      var store = new Store(new StoreOptions { DataBaseName = "vectors", DataBasePath = basePath });
      try
      {
        await test(store);
      }
      finally
      {
        await store.Close();
      }
    }
    finally
    {
      if (Directory.Exists(basePath))
        Directory.Delete(basePath, true);
    }
  }

  [Fact]
  public async Task Default_options_do_not_throw_and_use_the_options_default_name()
  {
    await WithStore(async store =>
    {
      // Before, constructing without options meant a NullReferenceException on
      // the first Add (options.DocumentSerializer on a null options).
      var collection = new VectorCollection<Article>(store);
      await collection.AddAsync(VectorKey.From(1), new Article { Title = "one" }, UnitVector(8, 1));
      await store.SaveChangesAsync();

      // Consistent with the client and with explicitly-defaulted options.
      Assert.Contains(typeof(Article).Namespace + "." + typeof(Article).Name, store.GetCollections());
      Assert.True(collection.TryGetValue(VectorKey.From(1), out var entry));
      Assert.Equal("one", entry.Content.Title);
    });
  }

  [Fact]
  public async Task Vector_search_with_predicate_and_embedding_readback()
  {
    await WithStore(async store =>
    {
      var collection = new VectorCollection<Article>(store, new VectorCollectionOptions<Article> { Name = "articles" });

      for (var i = 0; i < 8; i++)
        await collection.AddAsync(VectorKey.From(i), new Article
        {
          Title = $"article {i}",
          Category = i % 2 == 0 ? "news" : "blog"
        }, UnitVector(8, i));

      await store.SaveChangesAsync();

      // Plain vector search: exact hit first.
      var results = collection.Search(UnitVector(8, 3), top: 3);
      Assert.Equal(3, BitConverter.ToInt32(results[0].Key.Value));
      Assert.Equal("article 3", results[0].Content.Title);
      Assert.True(results[0].Score > 0.99f);

      // Predicate: the best match (3, "blog") must be excluded.
      var newsOnly = collection.Search(UnitVector(8, 3), top: 3, predicate: a => a.Category == "news");
      Assert.NotEmpty(newsOnly);
      Assert.All(newsOnly, r => Assert.Equal("news", r.Content.Category));
      Assert.DoesNotContain(newsOnly, r => BitConverter.ToInt32(r.Key.Value) == 3);

      // Stored embedding readback.
      var embedding = collection.GetEmbedding(VectorKey.From(5));
      Assert.Equal(UnitVector(8, 5), embedding);
      Assert.Null(collection.GetEmbedding(VectorKey.From(999)));
    });
  }

  [Fact]
  public async Task Sentence_overloads_use_the_configured_embedder()
  {
    await WithStore(async store =>
    {
      // Fake embedder: maps a sentence to a deterministic unit vector.
      var collection = new VectorCollection<Article>(store, new VectorCollectionOptions<Article>
      {
        Name = "sentences",
        SentenceEmbedder = sentence => UnitVector(8, sentence.Length)
      });

      await collection.AddAsync(VectorKey.From(1), new Article { Title = "aa" }, sentence: "aa");
      await collection.AddAsync(VectorKey.From(2), new Article { Title = "bbbb" }, sentence: "bbbb");
      await store.SaveChangesAsync();

      var results = collection.Search("cc", top: 1); // same length as "aa" -> same vector
      Assert.Single(results);
      Assert.Equal("aa", results[0].Content.Title);

      // Without an embedder the sentence overloads must fail loudly.
      var withoutEmbedder = new VectorCollection<Article>(store, new VectorCollectionOptions<Article> { Name = "sentences" });
      Assert.Throws<InvalidOperationException>(() => withoutEmbedder.Search("x", top: 1));
    });
  }

  [Fact]
  public async Task Async_add_remove_clear_roundtrip()
  {
    await WithStore(async store =>
    {
      var collection = new VectorCollection<Article>(store, new VectorCollectionOptions<Article> { Name = "asy" });

      await collection.AddAsync(VectorKey.From(1), new Article { Title = "one" }, UnitVector(8, 1));
      await collection.AddAsync(VectorKey.From(2), new Article { Title = "two" }, UnitVector(8, 2));
      await store.SaveChangesAsync();
      Assert.Equal(2, collection.Count);

      Assert.True(await collection.RemoveAsync(VectorKey.From(1)));
      Assert.Equal(1, collection.Count);
      Assert.False(collection.ContainsKey(VectorKey.From(1)));

      await collection.ClearAsync();
      Assert.Equal(0, collection.Count);
    });
  }

  [Fact]
  public async Task Document_collection_without_options_and_async_surface()
  {
    await WithStore(async store =>
    {
      var documents = new DocumentCollection<Article>(store);

      await documents.AddAsync(VectorKey.From(1), new Article { Title = "doc" });
      await store.SaveChangesAsync();

      Assert.Contains(typeof(Article).Namespace + "." + typeof(Article).Name, store.GetCollections());
      Assert.True(documents.TryGetValue(VectorKey.From(1), out var doc));
      Assert.Equal("doc", doc.Title);

      Assert.True(await documents.RemoveAsync(VectorKey.From(1)));
      Assert.Equal(0, documents.Count);
    });
  }
}
