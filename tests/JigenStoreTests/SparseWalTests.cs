using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexers;

namespace JigenTests;

public class SparseWalTests
{
  private static string NewPath() =>
    Path.Combine(Path.GetTempPath(), $"jigen-sparse-wal-{Guid.NewGuid():N}");

  private static SparseVector Vector(params (int term, float weight)[] pairs) =>
    SparseVector.FromPairs(pairs.Select(p => new KeyValuePair<int, float>(p.term, p.weight)));

  [Fact]
  public void InsertRecord_RoundTripsTheSparseSection()
  {
    var id = Encoding.UTF8.GetBytes("key");
    var content = Encoding.UTF8.GetBytes("payload");
    var embedding = new float[] { 1f, 2f };
    var sparse = Vector((3, 0.5f), (9, 2f));

    var size = WalRecord.InsertRecordSize(id, "docs", content, embedding, sparse);
    var buffer = new byte[size];
    var written = WalRecord.SerializeInsert(buffer, id, "docs", content, embedding, sparse);
    Assert.Equal(size, written);

    using var stream = new MemoryStream(buffer);
    Assert.True(WalRecord.TryReadRecord(stream,
      out var type, out var readId, out var collection, out var readContent, out var readEmbedding,
      out var readSparse, out _, out _));

    Assert.Equal(WalRecordType.Insert, type);
    Assert.Equal(id, readId);
    Assert.Equal("docs", collection);
    Assert.Equal(content, readContent);
    Assert.Equal(embedding, readEmbedding);
    Assert.NotNull(readSparse);
    Assert.Equal([3, 9], readSparse.Terms.ToArray());
    Assert.Equal([0.5f, 2f], readSparse.Weights.ToArray());
  }

  [Fact]
  public void InsertRecord_WithoutSparseVector_StaysReadable()
  {
    var id = Encoding.UTF8.GetBytes("key");
    var size = WalRecord.InsertRecordSize(id, "docs", null, new float[] { 1f });
    var buffer = new byte[size];
    WalRecord.SerializeInsert(buffer, id, "docs", null, new float[] { 1f });

    using var stream = new MemoryStream(buffer);
    Assert.True(WalRecord.TryReadRecord(stream,
      out _, out _, out _, out _, out _, out var sparse, out _, out _));

    Assert.Null(sparse);
  }

  [Fact]
  public void InsertRecord_WithTruncatedSparseSection_IsRejected()
  {
    var id = Encoding.UTF8.GetBytes("key");
    var sparse = Vector((3, 0.5f), (9, 2f));
    var size = WalRecord.InsertRecordSize(id, "docs", null, null, sparse);
    var buffer = new byte[size];
    WalRecord.SerializeInsert(buffer, id, "docs", null, null, sparse);

    // Cut one byte out of the pairs: the record must not be half-applied.
    // The length header keeps its original value, exactly like a torn write.
    var truncated = buffer.AsSpan(0, buffer.Length - 1).ToArray();

    using var stream = new MemoryStream(truncated);
    Assert.False(WalRecord.TryReadRecord(stream,
      out _, out _, out _, out _, out _, out _, out _, out _));
  }

  [Fact]
  public async Task CommittedTransaction_PersistsSparseVectors()
  {
    var path = NewPath();
    Directory.CreateDirectory(path);
    try
    {
      var options = new StoreOptions
      {
        DataBaseName = "tx",
        DataBasePath = path,
        Wal = new WalOptions { Enabled = true },
        SparseIndex = new SparseIndex()
      };

      using (var store = new Store(options))
      {
        await using var transaction = store.BeginTransaction();
        transaction.Append(new VectorEntry
        {
          Id = Encoding.UTF8.GetBytes("a"),
          CollectionName = "docs",
          Content = Encoding.UTF8.GetBytes("a"),
          Embedding = new float[] { 1f },
          Sparse = Vector((4, 1.25f))
        });
        await transaction.CommitAsync();
        await store.SaveChangesAsync();
        await store.Close();
      }

      using (var reopened = new Store(options))
      {
        var read = reopened.GetSparse("docs", Encoding.UTF8.GetBytes("a"));
        Assert.NotNull(read);
        Assert.Equal(4, read.Terms.Span[0]);
        Assert.Equal(1.25f, read.Weights.Span[0]);
      }
    }
    finally
    {
      Directory.Delete(path, recursive: true);
    }
  }
}
