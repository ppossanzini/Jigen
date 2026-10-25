using Jigen.Client;
using Jigen.Client.BaseTypes;
using JigenClientTest.Model;

namespace JigenClientTest;

/// <summary>
/// Round trip against a running server: the sparse pairs must survive the
/// transport of the write and come back from a sparse query. The collection is
/// dedicated to this test, so clearing it does not touch the other data of the
/// shared database.
/// </summary>
public class SparseRoundTripTests
{
  private const int DocumentTerm = 4242;

  [ServerFact(database: "Test")]
  public void Written_sparse_terms_are_found_by_a_sparse_search()
  {
    var context = new TestContext(new ConnectionOptions
    {
      HostName = "localhost",
      Port = 3223,
      TLS = false,
      DatabaseName = "Test"
    });

    var collection = context.Collection<Entity1>("sparse-round-trip");
    collection.Clear();

    collection.Add(1, new Entity1 { Title = "sparse one" }, [0.1f, 0.2f],
      new Dictionary<int, float> { [DocumentTerm] = 1.5f });
    collection.Add(2, new Entity1 { Title = "no sparse" }, [0.3f, 0.4f]);

    var hits = collection.SearchSparse(new Dictionary<int, float> { [DocumentTerm] = 1f });

    var hit = Assert.Single(hits);
    Assert.Equal(1, BitConverter.ToInt32(hit.Key.Value));
    Assert.NotNull(hit.SparseScore);

    collection.Clear();
  }

  private sealed class TestContext : Context
  {
    public TestContext(ConnectionOptions options) : base(options)
    {
    }
  }
}
