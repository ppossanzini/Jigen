using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

/// <summary>
/// The management view of a collection must report the sparse index, not only
/// the dense one: an administrator reading the collection info has to see that
/// the sparse side exists, how many segments it reads and how much space it takes.
/// </summary>
public class SparseCollectionInfoTests
{
  private const string Collection = "articles";

  private static SparseTerm[] Terms(params (int Term, float Weight)[] pairs) =>
    pairs.Select(pair => new SparseTerm(pair.Term, pair.Weight)).ToArray();

  private static async Task WithStore(Func<Store, Task> test)
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-sparseinfo-{Guid.NewGuid():N}");
    Directory.CreateDirectory(basePath);

    try
    {
      // Every batch is committed, so each SaveChanges closes one segment.
      var store = new Store(new StoreOptions
      {
        DataBaseName = "sparseinfo", DataBasePath = basePath,
        Sparse = new SparseOptions { CommitMinTerms = 1, MaintenanceInterval = TimeSpan.Zero }
      });
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

  private static VectorEntry Entry(int key, float[] embeddings, SparseTerm[] sparse) => new()
  {
    Id = BitConverter.GetBytes(key),
    CollectionName = Collection,
    Content = BitConverter.GetBytes(key),
    Embeddings = [embeddings],
    SparseVectors = sparse is null ? [] : [sparse]
  };

  [Fact]
  public async Task Collection_info_is_null_for_the_sparse_index_when_there_is_none()
  {
    await WithStore(async store =>
    {
      await store.AppendContent(Entry(1, [1f, 0f], null));
      await store.SaveChangesAsync();

      var info = store.GetCollectionInfo(Collection);

      Assert.Null(info.Sparse);
      Assert.Null(store.GetSparseInfo(Collection));
    });
  }

  [Fact]
  public async Task Collection_info_reports_segments_terms_blocks_and_size()
  {
    await WithStore(async store =>
    {
      await store.AppendContent(Entry(1, [1f, 0f], Terms((7, 1f), (11, 0.5f))));
      await store.AppendContent(Entry(2, [0f, 1f], Terms((7, 1f))));
      await store.SaveChangesAsync();

      var first = store.GetCollectionInfo(Collection).Sparse;
      Assert.NotNull(first);
      Assert.Equal(1, first.Segments);
      Assert.Equal(2, first.Terms);
      Assert.True(first.Blocks > 0);
      Assert.True(first.SizeBytes > 0);
      Assert.Equal(0, first.Tombstones);

      // A second batch closes another segment: the searches read both until a
      // shrink compacts them.
      await store.AppendContent(Entry(3, [1f, 1f], Terms((13, 1f))));
      await store.SaveChangesAsync();

      var second = store.GetCollectionInfo(Collection).Sparse;
      Assert.NotNull(second);
      Assert.Equal(2, second.Segments);
      Assert.Equal(3, second.Terms);
      Assert.True(second.SizeBytes >= first.SizeBytes);
    });
  }

  [Fact]
  public async Task Collection_info_reports_retired_documents_until_the_shrink()
  {
    await WithStore(async store =>
    {
      await store.AppendContent(Entry(1, [1f, 0f], Terms((7, 1f))));
      await store.AppendContent(Entry(2, [0f, 1f], Terms((11, 1f))));
      await store.SaveChangesAsync();

      await store.DeleteContent(Collection, BitConverter.GetBytes(1));
      await store.SaveChangesAsync();

      var beforeShrink = store.GetCollectionInfo(Collection).Sparse;
      Assert.NotNull(beforeShrink);
      Assert.True(beforeShrink.Tombstones > 0);

      await store.ShrinkAsync();
      await store.SaveChangesAsync();

      var afterShrink = store.GetCollectionInfo(Collection).Sparse;
      Assert.NotNull(afterShrink);
      Assert.Equal(1, afterShrink.Segments);
      Assert.Equal(0, afterShrink.Tombstones);
    });
  }
}
