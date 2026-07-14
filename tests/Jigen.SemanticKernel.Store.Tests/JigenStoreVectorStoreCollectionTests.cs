using Jigen;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Store.Tests;

public class JigenStoreVectorStoreCollectionTests
{
  private static float[] UnitVector(int dimensions, int direction)
  {
    var vector = new float[dimensions];
    vector[direction % dimensions] = 1f;
    return vector;
  }

  private static async Task WithStore(Func<global::Jigen.Store, Task> test)
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-sk-store-{Guid.NewGuid():N}");
    Directory.CreateDirectory(basePath);

    try
    {
      var store = new global::Jigen.Store(new StoreOptions { DataBaseName = "vectors", DataBasePath = basePath });
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

  private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
  {
    var result = new List<T>();
    await foreach (var item in source)
      result.Add(item);
    return result;
  }

  [Fact]
  public async Task Upsert_then_get_roundtrips_content_and_key()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("articles");

      var id = Guid.NewGuid();
      await collection.UpsertAsync(new Article { Id = id, Title = "one", Category = "news", Embedding = UnitVector(8, 1) });

      var fetched = await collection.GetAsync(id);
      Assert.NotNull(fetched);
      Assert.Equal(id, fetched!.Id);
      Assert.Equal("one", fetched.Title);
      Assert.Equal("news", fetched.Category);
      // Vector not requested: default RecordRetrievalOptions must not populate it.
      Assert.True(fetched.Embedding.IsEmpty);
    });
  }

  [Fact]
  public async Task Get_with_IncludeVectors_returns_the_stored_embedding()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("articles");

      var id = Guid.NewGuid();
      var vector = UnitVector(8, 3);
      await collection.UpsertAsync(new Article { Id = id, Title = "vec", Embedding = vector });

      var fetched = await collection.GetAsync(id, new RecordRetrievalOptions { IncludeVectors = true });
      Assert.NotNull(fetched);
      Assert.Equal(vector, fetched!.Embedding.ToArray());
    });
  }

  [Fact]
  public async Task Get_missing_key_returns_null()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("articles");

      var fetched = await collection.GetAsync(Guid.NewGuid());
      Assert.Null(fetched);
    });
  }

  [Fact]
  public async Task Delete_removes_the_entry()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("articles");

      var id = Guid.NewGuid();
      await collection.UpsertAsync(new Article { Id = id, Title = "gone", Embedding = UnitVector(8, 1) });
      Assert.NotNull(await collection.GetAsync(id));

      await collection.DeleteAsync(id);
      Assert.Null(await collection.GetAsync(id));
    });
  }

  [Fact]
  public async Task Upsert_many_inserts_every_record()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("bulk");

      var records = Enumerable.Range(0, 5)
        .Select(i => new Article { Id = Guid.NewGuid(), Title = $"article {i}", Embedding = UnitVector(8, i) })
        .ToList();

      await collection.UpsertAsync(records);

      foreach (var record in records)
      {
        var fetched = await collection.GetAsync(record.Id);
        Assert.NotNull(fetched);
        Assert.Equal(record.Title, fetched!.Title);
      }
    });
  }

  [Fact]
  public async Task Search_without_filter_returns_the_closest_match_first()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("search");

      var ids = new Guid[8];
      for (var i = 0; i < 8; i++)
      {
        ids[i] = Guid.NewGuid();
        await collection.UpsertAsync(new Article { Id = ids[i], Title = $"article {i}", Category = i % 2 == 0 ? "news" : "blog", Embedding = UnitVector(8, i) });
      }

      var results = await ToListAsync(collection.SearchAsync(UnitVector(8, 3), top: 3));

      Assert.Equal(3, results.Count);
      Assert.Equal(ids[3], results[0].Record.Id);
      Assert.Equal("article 3", results[0].Record.Title);
      Assert.True(results[0].Score > 0.99);
    });
  }

  [Fact]
  public async Task Search_with_filter_excludes_non_matching_records()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("search-filtered");

      var ids = new Guid[8];
      for (var i = 0; i < 8; i++)
      {
        ids[i] = Guid.NewGuid();
        await collection.UpsertAsync(new Article { Id = ids[i], Title = $"article {i}", Category = i % 2 == 0 ? "news" : "blog", Embedding = UnitVector(8, i) });
      }

      // The exact match for direction 3 is a "blog" (odd index): a "news"-only
      // filter must exclude it even though it would otherwise win outright.
      var options = new VectorSearchOptions<Article> { Filter = a => a.Category == "news" };
      var results = await ToListAsync(collection.SearchAsync(UnitVector(8, 3), top: 3, options));

      Assert.NotEmpty(results);
      Assert.All(results, r => Assert.Equal("news", r.Record.Category));
      Assert.DoesNotContain(results, r => r.Record.Id == ids[3]);
    });
  }

  [Fact]
  public async Task CollectionExists_reflects_writes_and_EnsureCollectionDeleted_clears_it()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("lifecycle");

      Assert.False(await collection.CollectionExistsAsync());

      await collection.UpsertAsync(new Article { Id = Guid.NewGuid(), Title = "x", Embedding = UnitVector(8, 1) });
      Assert.True(await collection.CollectionExistsAsync());

      // No-op per Jigen's implicit-creation semantics; must not throw.
      await collection.EnsureCollectionExistsAsync();

      await collection.EnsureCollectionDeletedAsync();
      Assert.False(await collection.CollectionExistsAsync());
    });
  }

  [Fact]
  public async Task VectorStore_level_CollectionExists_and_EnsureCollectionDeleted_agree_with_the_collection_level_ones()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      const string name = "vs-lifecycle";
      var collection = vectorStore.GetCollection<Guid, Article>(name);

      Assert.False(await vectorStore.CollectionExistsAsync(name));
      Assert.DoesNotContain(name, await ToListAsync(vectorStore.ListCollectionNamesAsync()));

      await collection.UpsertAsync(new Article { Id = Guid.NewGuid(), Title = "x", Embedding = UnitVector(8, 1) });

      Assert.True(await vectorStore.CollectionExistsAsync(name));
      Assert.Contains(name, await ToListAsync(vectorStore.ListCollectionNamesAsync()));

      await vectorStore.EnsureCollectionDeletedAsync(name);
      Assert.False(await vectorStore.CollectionExistsAsync(name));
    });
  }

  [Fact]
  public async Task String_keys_roundtrip()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<string, ArticleWithStringKey>("string-keyed");

      await collection.UpsertAsync(new ArticleWithStringKey { Slug = "hello-world", Title = "Hello, world", Embedding = UnitVector(8, 2) });

      var fetched = await collection.GetAsync("hello-world");
      Assert.NotNull(fetched);
      Assert.Equal("Hello, world", fetched!.Title);

      Assert.Null(await collection.GetAsync("does-not-exist"));
    });
  }

  [Fact]
  public async Task GetAsync_with_predicate_scans_and_filters_client_side()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      var collection = vectorStore.GetCollection<Guid, Article>("predicate-scan");

      for (var i = 0; i < 5; i++)
        await collection.UpsertAsync(new Article { Id = Guid.NewGuid(), Title = $"article {i}", Category = i % 2 == 0 ? "news" : "blog", Embedding = UnitVector(8, i) });

      var newsOnly = await ToListAsync(collection.GetAsync(a => a.Category == "news", top: 10));

      Assert.NotEmpty(newsOnly);
      Assert.All(newsOnly, a => Assert.Equal("news", a.Category));
    });
  }

  [Fact]
  public void Record_without_a_vector_property_throws_on_collection_construction()
  {
    Assert.Throws<ArgumentException>(() =>
    {
      _ = Jigen.SemanticKernel.Abstractions.JigenRecordModel<Guid, NoVectorRecord>.Instance;
    });
  }

  [Fact]
  public void Record_with_two_vector_properties_throws_on_collection_construction()
  {
    Assert.Throws<ArgumentException>(() =>
    {
      _ = Jigen.SemanticKernel.Abstractions.JigenRecordModel<Guid, TwoVectorsRecord>.Instance;
    });
  }

  [Fact]
  public void Record_without_a_key_property_throws_on_collection_construction()
  {
    Assert.Throws<ArgumentException>(() =>
    {
      _ = Jigen.SemanticKernel.Abstractions.JigenRecordModel<Guid, NoKeyRecord>.Instance;
    });
  }

  [Fact]
  public void Unsupported_key_type_throws_NotSupportedException()
  {
    Assert.Throws<NotSupportedException>(() =>
    {
      _ = Jigen.SemanticKernel.Abstractions.JigenRecordModel<int, IntKeyRecord>.Instance;
    });
  }

  [Fact]
  public async Task GetCollection_with_unsupported_key_type_throws_via_the_VectorStore_entry_point()
  {
    await WithStore(async store =>
    {
      var vectorStore = new JigenStoreVectorStore(store);
      Assert.Throws<NotSupportedException>(() => vectorStore.GetCollection<int, IntKeyRecord>("bad"));
      await Task.CompletedTask;
    });
  }
}
