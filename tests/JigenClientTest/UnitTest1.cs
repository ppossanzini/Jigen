using System.Diagnostics;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using JigenClientTest.Model;
using Xunit.Abstractions;

namespace JigenClientTest;

public class UnitTest1
{
  private readonly ITestOutputHelper _testOutputHelper;
  private DB db;

  public UnitTest1(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    db = new DB(new ConnectionOptions()
    {
      ConnectionString = "http://localhost:5001",
      DatabaseName = "Test"
    });
  }

  [Fact]
  public void Insert()
  {
    db.Sentences.Add(1, new VectorEntry<Entity1>()
    {
      Key = 1, Content = new Entity1() { Id = Guid.NewGuid(), Sentence = "blablabla", Title = "allora..." }, Embedding = Array.Empty<float>()
    });
  }
  
  [Theory]
  [InlineData(1)]
  [InlineData(10)]
  [InlineData(100)]
  [InlineData(1000)]
  [InlineData(10_000)]
  [InlineData(100_000)]
  [InlineData(1_000_000)]
  public void Insert2(int count)
  {

    _testOutputHelper.WriteLine($"Inserting {count}");
    var sw = new Stopwatch();
    sw.Start();
    
    for (int i = 0; i < count; i++)
    {
      
      db.Sentences.Add(i, new VectorEntry<Entity1>()
      {
        Key = i, Content = new Entity1() { Id = Guid.NewGuid(), Sentence = "blablabla", Title = "allora..." }, Embedding = Array.Empty<float>()
      });
    }
    
    sw.Stop();
    _testOutputHelper.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
  }

}