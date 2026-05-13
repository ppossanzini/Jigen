using Jigen;
using Jigen.Indexer;
using Xunit.Abstractions;

namespace HnswTest;

public class IndexingTestBase
{
  private readonly ITestOutputHelper _testOutputHelper;
  private readonly Store _store;

  public IndexingTestBase(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "tests",
      DataBasePath = "/data/jigendb",
      Indexer = new SmallWorldIndexer(new  SmallWorldOptions()
      {
        M = 16,
        EfConstruction = 200,
        EfSearch = 50,
        StoragePath = "/data/jigendb/hnsw"
      })
    });
  }
}