using System.Diagnostics;
using Jigen.Client;
using Jigen.Client.BaseTypes;
using Jigen.Proto;
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
      HostName = "localhost",
      Port = 3223,
      TLS = false,
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
        Key = i, 
        Content = new Entity1() { Id = Guid.NewGuid(), Sentence = "blablabla", Title = "allora..." }, Embedding = Array.Empty<float>()
      });
    }
    
    sw.Stop();
    _testOutputHelper.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
  }

  [Fact]
  public void SerializationRoundTripTest()
  {
    // Arrange
    var testKey = 9999;
    var testKeyVector = (VectorKey)testKey;
    var expectedEntity = new Entity1()
    {
      Id = Guid.NewGuid(),
      Title = "Serialization Test",
      Sentence = "tes tets"  // Space between words to verify exact match
    };

    _testOutputHelper.WriteLine($"Writing entity with Sentence: '{expectedEntity.Sentence}'");

    // Act: Write
    db.Sentences.Add(testKeyVector, new VectorEntry<Entity1>()
    {
      Key = testKeyVector,
      Content = expectedEntity,
      Embedding = Array.Empty<float>()
    });

    // Act: Read back
    _testOutputHelper.WriteLine($"Reading entity back with key: {testKey}");
    
    
    var success = db.Sentences.TryGetValue(testKeyVector, out var retrievedEntry);

    // Assert: Content matches exactly
    Assert.True(success, "Failed to retrieve the written entity");
    Assert.NotNull(retrievedEntry);
    Assert.NotNull(retrievedEntry.Content);

    var retrievedEntity = retrievedEntry.Content;
    _testOutputHelper.WriteLine($"Retrieved entity with Sentence: '{retrievedEntity.Sentence}'");

    Assert.Equal(expectedEntity.Sentence, retrievedEntity.Sentence);
    Assert.Equal(expectedEntity.Title, retrievedEntity.Title);
    Assert.Equal(expectedEntity.Id, retrievedEntity.Id);

    _testOutputHelper.WriteLine("✓ Serialization round-trip verified: content is identical");
  }

  [Fact]
  public void SearchBySemanticSimilarityTest()
  {
    // Arrange
    var testKey = 8888;
    var testKeyVector = (VectorKey)testKey;
    var indexedEntity = new Entity1()
    {
      Id = Guid.NewGuid(),
      Title = "Semantic Search Test",
      Sentence = "tes tets"
    };

    _testOutputHelper.WriteLine($"Indexing entity with Sentence: '{indexedEntity.Sentence}'");

    // Act: Write with sentence for embedding
    db.Sentences.Add(testKeyVector, new VectorEntry<Entity1>()
    {
      Key = testKeyVector,
      Content = indexedEntity,
      Embedding = db.ServiceClient.CalculateEmbeddings(new EmbeddingRequest(){Message = "test test test test"}).Embeddings.ToArray()
    });

    // Act: Search for semantically similar text
    var searchQuery = "test";
    _testOutputHelper.WriteLine($"Searching for: '{searchQuery}'");
    var results = db.Sentences.Search(searchQuery, top: 10);

    // Assert: Should find the indexed document
    _testOutputHelper.WriteLine($"Found {results.Count} results");
    Assert.NotEmpty(results);

    var found = results.FirstOrDefault(r => r.Key.Value.SequenceEqual(testKeyVector.Value));
    Assert.NotNull(found);
    Assert.NotNull(found.Content);

    var foundEntity = found.Content;
    Assert.Equal("tes tets", foundEntity.Sentence);

    _testOutputHelper.WriteLine($"✓ Semantic search verified: found '{foundEntity.Sentence}' when searching for '{searchQuery}'");
  }

}
