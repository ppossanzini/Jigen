using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

/// <summary>
/// A bulk load must not leave one sparse segment per ingestion batch: documents
/// are buffered until enough terms or enough time accumulate, and a background
/// worker compacts the collections that still read too many segments.
/// </summary>
public class SparseBufferingTests
{
  private const string Collection = "articles";

  private static SparseTerm[] Terms(params int[] terms) =>
    terms.Select(term => new SparseTerm(term, 1f)).ToArray();

  private static VectorEntry Entry(int key, params int[] terms) => new()
  {
    Id = BitConverter.GetBytes(key),
    CollectionName = Collection,
    Content = BitConverter.GetBytes(key),
    Embeddings = [new[] { 1f, 0f }],
    SparseVectors = [Terms(terms)]
  };

  private static async Task WithStore(SparseOptions sparse, Func<Store, StoreOptions, Task> test)
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-sparsebuffer-{Guid.CreateVersion7():N}");
    Directory.CreateDirectory(basePath);
    var options = new StoreOptions { DataBaseName = "sparsebuffer", DataBasePath = basePath, Sparse = sparse };

    try
    {
      var store = new Store(options);
      try
      {
        await test(store, options);
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

  private static async Task Ingest(Store store, params VectorEntry[] entries)
  {
    foreach (var entry in entries)
      await store.AppendContent(entry);
    await store.SaveChangesAsync();
  }

  private static int Segments(Store store) => store.GetSparseInfo(Collection)?.Segments ?? 0;

  private static int Hits(Store store, int term) =>
    store.SearchSparse(Collection, new Dictionary<int, float> { [term] = 1f }, 10).Count;

  private static async Task Eventually(Func<bool> condition)
  {
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (!condition())
    {
      Assert.True(DateTime.UtcNow < deadline, "The condition was not reached within 10 seconds.");
      await Task.Delay(25);
    }
  }

  [Fact]
  public async Task Batches_below_the_term_threshold_stay_buffered_until_flushed()
  {
    var sparse = new SparseOptions { CommitMinTerms = 1000, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 7, 11));
      await Ingest(store, Entry(2, 7));

      Assert.Equal(0, Segments(store));
      Assert.Equal(0, Hits(store, 7));

      store.FlushSparseIndexes();

      Assert.Equal(1, Segments(store));
      Assert.Equal(2, Hits(store, 7));
    });
  }

  [Fact]
  public async Task The_buffer_is_committed_once_it_holds_enough_distinct_terms()
  {
    var sparse = new SparseOptions { CommitMinTerms = 3, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 1, 2));
      Assert.Equal(0, Segments(store));

      await Ingest(store, Entry(2, 2, 3));
      Assert.Equal(1, Segments(store));
      Assert.Equal(1, Hits(store, 3));
    });
  }

  [Fact]
  public async Task The_worker_commits_a_buffer_older_than_the_maximum_delay()
  {
    var sparse = new SparseOptions
    {
      CommitMinTerms = 1000,
      CommitMaxDelay = TimeSpan.FromMilliseconds(300),
      MaintenanceInterval = TimeSpan.FromMilliseconds(50)
    };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 7));

      await Eventually(() => Hits(store, 7) == 1);
      Assert.Equal(1, Segments(store));
    });
  }

  [Fact]
  public async Task The_worker_merges_small_segments_by_size()
  {
    var sparse = new SparseOptions
    {
      CommitMinTerms = 1,
      MinSegmentBytes = 1024 * 1024,
      MaintenanceInterval = TimeSpan.FromMilliseconds(50)
    };
    await WithStore(sparse, async (store, _) =>
    {
      for (var key = 1; key <= 5; key++)
        await Ingest(store, Entry(key, 7, key + 100));

      // Every segment is far below the floor, so they all end up in one.
      await Eventually(() => Segments(store) == 1);
      Assert.Equal(5, Hits(store, 7));
      Assert.Equal(1, Hits(store, 103));
    });
  }

  [Fact]
  public async Task The_worker_leaves_alone_segments_at_the_maximum_size()
  {
    var sparse = new SparseOptions
    {
      CommitMinTerms = 1,
      MaxSegmentBytes = 1,
      MaintenanceInterval = TimeSpan.FromMilliseconds(50)
    };
    await WithStore(sparse, async (store, _) =>
    {
      for (var key = 1; key <= 3; key++)
        await Ingest(store, Entry(key, 7));

      await Task.Delay(300);
      Assert.Equal(3, Segments(store));
      Assert.Equal(3, Hits(store, 7));
    });
  }

  [Fact]
  public async Task A_partial_merge_keeps_deletes_hidden_in_the_segments_left_out()
  {
    var sparse = new SparseOptions { CommitMinTerms = 1, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 7));
      await Ingest(store, Entry(2, 7));
      await Ingest(store, Entry(3, 7));
      await store.DeleteContent(Collection, BitConverter.GetBytes(1));
      await store.SaveChangesAsync();
      await store.Close();

      var path = Directory.EnumerateDirectories(store.SparsePath).Single();
      var generations = Jigen.Index.Sparse.SparseCollectionManifest.Read(path);
      new Jigen.Index.Sparse.SparseCollectionCompactor(path).CompactSegments(generations.Skip(1));

      using var storage = new AsyncDisposableBox(Jigen.Index.Sparse.SparseCollectionStorage.Open(path));
      Assert.Equal(2, storage.Value.Index.Segments.Count);
      var hits = storage.Value.Index.Search(new Dictionary<int, float> { [7] = 1f }, 10);
      Assert.Equal(2, hits.Count);
      Assert.DoesNotContain(hits, hit => hit.DocumentId.SequenceEqual(BitConverter.GetBytes(1)));
    });
  }

  private sealed class AsyncDisposableBox(Jigen.Index.Sparse.SparseCollectionStorage value) : IDisposable
  {
    public Jigen.Index.Sparse.SparseCollectionStorage Value { get; } = value;
    public void Dispose() => Value.DisposeAsync().AsTask().GetAwaiter().GetResult();
  }

  [Theory]
  [InlineData(new long[] { 1, 1 }, 0, new long[] { 1, 2 })]
  [InlineData(new long[] { 4, 2, 1 }, 0, new long[0])]
  [InlineData(new long[] { 4, 2, 1, 1 }, 0, new long[] { 1, 2, 3, 4 })]
  [InlineData(new long[] { 8, 2, 1, 1 }, 0, new long[] { 2, 3, 4 })]
  [InlineData(new long[] { 100, 10 }, 0, new long[0])]
  [InlineData(new long[] { 100, 10 }, 200, new long[] { 1, 2 })]
  [InlineData(new long[] { 1000, 1, 1 }, 0, new long[] { 2, 3 })]
  public void The_merge_policy_selects_peers_of_similar_size(long[] sizes, long floor, long[] expected)
  {
    var segments = sizes.Select((bytes, index) => ((long)index + 1, bytes)).ToArray();

    var selected = Jigen.Index.Sparse.SparseMergePolicy.Select(segments, floor, maxSegmentBytes: 1000);

    Assert.Equal(expected, selected);
  }

  [Fact]
  public async Task Overwriting_a_buffered_document_keeps_only_the_new_version()
  {
    var sparse = new SparseOptions { CommitMinTerms = 1000, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 7));
      await Ingest(store, Entry(1, 11));

      Assert.Equal(0, Segments(store));

      store.FlushSparseIndexes();

      Assert.Equal(0, Hits(store, 7));
      Assert.Equal(1, Hits(store, 11));
    });
  }

  [Fact]
  public async Task Deleting_a_document_hides_the_committed_copy_without_committing_the_buffer()
  {
    var sparse = new SparseOptions { CommitMinTerms = 2, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, _) =>
    {
      await Ingest(store, Entry(1, 7, 8));
      await Ingest(store, Entry(2, 7));
      Assert.Equal(1, Segments(store));

      await store.DeleteContent(Collection, BitConverter.GetBytes(1));
      await store.SaveChangesAsync();

      Assert.Equal(1, Segments(store));
      Assert.Equal(0, Hits(store, 8));

      store.FlushSparseIndexes();
      Assert.Equal(1, Hits(store, 7));
    });
  }

  [Fact]
  public async Task Closing_the_store_commits_the_buffer()
  {
    var sparse = new SparseOptions { CommitMinTerms = 1000, MaintenanceInterval = TimeSpan.Zero };
    await WithStore(sparse, async (store, options) =>
    {
      await Ingest(store, Entry(1, 7));
      await store.Close();

      var reopened = new Store(options);
      try
      {
        Assert.Equal(1, Hits(reopened, 7));
      }
      finally
      {
        await reopened.Close();
      }
    });
  }
}
