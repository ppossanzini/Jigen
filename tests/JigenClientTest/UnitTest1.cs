using Jigen.Client;
using Jigen.Client.BaseTypes;
using JigenClientTest.Model;

namespace JigenClientTest;

public class UnitTest1
{
  private DB db;

  public UnitTest1()
  {
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
}