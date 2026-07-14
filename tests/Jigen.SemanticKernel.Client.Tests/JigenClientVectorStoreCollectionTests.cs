using Jigen.Client;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Client.Tests;

/// <summary>
/// Exercises the gRPC-backed connector end to end.
/// </summary>
/// <remarks>
/// <para>
/// These tests need a live Jigen server reachable at localhost:3223 (the same
/// assumption <c>tests/JigenClientTest</c> makes elsewhere in this repo —
/// there is no in-process/embedded way to stand up <c>src/Server/Jigen</c>
/// for a test run, it composes Hikyaku.Kaido modules, RabbitMQ and identity
/// storage at startup). Run <c>docs/server</c>'s setup (or the Docker image)
/// against port 3223 before running this project; with no server listening
/// every test here fails fast with an RpcException ("connection refused")
/// rather than hanging. Verified locally: `dotnet run --project src/Server/Jigen`
/// (NOT `Jigen-AllInOne`, whose gRPC reference is commented out) with
/// `JigenServer__DataFolderPath` pointed at an existing empty folder starts
/// cleanly with no RabbitMQ/ONNX model needed — `Kaido:Enabled` defaults to
/// false and the gRPC endpoints carry no `[Authorize]`.
/// </para>
/// <para>
/// The <see cref="DatabaseName"/> database itself must already exist on the
/// server — unlike collections, Jigen's server does not create databases
/// implicitly on first write (<c>ArgumentException: "Database not found"</c>
/// otherwise). Create it once with:
/// <c>curl -X POST "http://localhost:13223/api/database?name=sk-client-tests"</c>
/// (or the equivalent `DatabasesManager`/`DatabaseController` call).
/// </para>
/// <para>
/// The record-validation tests at the bottom (missing/duplicate
/// [VectorStoreVector], unsupported key type) do NOT need a server: they
/// fail during collection construction, before any RPC is made.
/// </para>
/// </remarks>
public class JigenClientVectorStoreCollectionTests
{
  private const string DatabaseName = "sk-client-tests";

  private static float[] UnitVector(int dimensions, int direction)
  {
    var vector = new float[dimensions];
    vector[direction % dimensions] = 1f;
    return vector;
  }

  private static Context NewContext() => new(new ConnectionOptions
  {
    HostName = "localhost",
    Port = 3223,
    TLS = false,
    DatabaseName = DatabaseName
  });

  private static string UniqueCollectionName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

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
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("articles"));

    var id = Guid.NewGuid();
    await collection.UpsertAsync(new Article { Id = id, Title = "one", Category = "news", Embedding = UnitVector(8, 1) });

    var fetched = await collection.GetAsync(id);
    Assert.NotNull(fetched);
    Assert.Equal(id, fetched!.Id);
    Assert.Equal("one", fetched.Title);
    Assert.Equal("news", fetched.Category);
  }

  [Fact]
  public async Task Get_with_IncludeVectors_returns_the_stored_embedding()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("articles"));

    var id = Guid.NewGuid();
    var vector = UnitVector(8, 3);
    await collection.UpsertAsync(new Article { Id = id, Title = "vec", Embedding = vector });

    var fetched = await collection.GetAsync(id, new RecordRetrievalOptions { IncludeVectors = true });
    Assert.NotNull(fetched);
    Assert.Equal(vector, fetched!.Embedding.ToArray());
  }

  [Fact]
  public async Task Get_missing_key_returns_null()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("articles"));

    var fetched = await collection.GetAsync(Guid.NewGuid());
    Assert.Null(fetched);
  }

  [Fact]
  public async Task Delete_removes_the_entry()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("articles"));

    var id = Guid.NewGuid();
    await collection.UpsertAsync(new Article { Id = id, Title = "gone", Embedding = UnitVector(8, 1) });
    Assert.NotNull(await collection.GetAsync(id));

    await collection.DeleteAsync(id);
    Assert.Null(await collection.GetAsync(id));
  }

  [Fact]
  public async Task Upsert_many_inserts_every_record_over_the_bulk_streaming_rpc()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("bulk"));

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
  }

  [Fact]
  public async Task Search_without_filter_returns_the_closest_match_first()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("search"));

    var ids = new Guid[8];
    for (var i = 0; i < 8; i++)
    {
      ids[i] = Guid.NewGuid();
      await collection.UpsertAsync(new Article { Id = ids[i], Title = $"article {i}", Category = i % 2 == 0 ? "news" : "blog", Embedding = UnitVector(8, i) });
    }

    var results = await ToListAsync(collection.SearchAsync(UnitVector(8, 3), top: 3));

    Assert.Equal(3, results.Count);
    Assert.Equal(ids[3], results[0].Record.Id);
    Assert.True(results[0].Score > 0.99);
  }

  [Fact]
  public async Task Search_with_filter_excludes_non_matching_records()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<Guid, Article>(UniqueCollectionName("search-filtered"));

    var ids = new Guid[8];
    for (var i = 0; i < 8; i++)
    {
      ids[i] = Guid.NewGuid();
      await collection.UpsertAsync(new Article { Id = ids[i], Title = $"article {i}", Category = i % 2 == 0 ? "news" : "blog", Embedding = UnitVector(8, i) });
    }

    var options = new VectorSearchOptions<Article> { Filter = a => a.Category == "news" };
    var results = await ToListAsync(collection.SearchAsync(UnitVector(8, 3), top: 3, options));

    Assert.NotEmpty(results);
    Assert.All(results, r => Assert.Equal("news", r.Record.Category));
    Assert.DoesNotContain(results, r => r.Record.Id == ids[3]);
  }

  [Fact]
  public async Task CollectionExists_reflects_writes_and_EnsureCollectionDeleted_clears_it()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var name = UniqueCollectionName("lifecycle");
    var collection = vectorStore.GetCollection<Guid, Article>(name);

    Assert.False(await vectorStore.CollectionExistsAsync(name));

    await collection.UpsertAsync(new Article { Id = Guid.NewGuid(), Title = "x", Embedding = UnitVector(8, 1) });
    Assert.True(await vectorStore.CollectionExistsAsync(name));

    await collection.EnsureCollectionExistsAsync(); // no-op, must not throw

    await vectorStore.EnsureCollectionDeletedAsync(name);
    Assert.False(await vectorStore.CollectionExistsAsync(name));
  }

  [Fact]
  public async Task String_keys_roundtrip()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    var collection = vectorStore.GetCollection<string, ArticleWithStringKey>(UniqueCollectionName("string-keyed"));

    await collection.UpsertAsync(new ArticleWithStringKey { Slug = "hello-world", Title = "Hello, world", Embedding = UnitVector(8, 2) });

    var fetched = await collection.GetAsync("hello-world");
    Assert.NotNull(fetched);
    Assert.Equal("Hello, world", fetched!.Title);

    Assert.Null(await collection.GetAsync("does-not-exist"));
  }

  // -- The following do not need a live server: they fail at collection
  // construction time, before any RPC. --

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
  public void GetCollection_with_unsupported_key_type_throws_via_the_VectorStore_entry_point()
  {
    var vectorStore = new JigenClientVectorStore(NewContext());
    Assert.Throws<NotSupportedException>(() => vectorStore.GetCollection<int, IntKeyRecord>("bad"));
  }
}
