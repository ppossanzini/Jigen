using Jigen;
using Jigen.DataStructures;

namespace JigenTests;

public class DocumentCollectionSearchTests
{
  public sealed class SearchDocument
  {
    public string Title { get; set; }
    public List<string> Tags { get; set; }
    public SearchMetadata Metadata { get; set; }
  }

  public sealed class SearchMetadata
  {
    public string Category { get; set; }
    public string Country { get; set; }
  }

  [Fact]
  public async Task Search_filters_serialized_documents_with_expression()
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-doc-search-{Guid.NewGuid():N}");
    Directory.CreateDirectory(basePath);

    try
    {
      var store = new Store(new StoreOptions
      {
        DataBaseName = "documents",
        DataBasePath = basePath
      });

      var documents = new DocumentCollection<SearchDocument>(store, new DocumentCollectionOptions<SearchDocument>
      {
        Name = "docs"
      });

      documents.Add(1, new SearchDocument
      {
        Title = "Filtered document",
        Tags = ["science", "linq"],
        Metadata = new SearchMetadata
        {
          Category = "article",
          Country = "IT"
        }
      });

      documents.Add(2, new SearchDocument
      {
        Title = "Excluded document",
        Tags = ["music"],
        Metadata = new SearchMetadata
        {
          Category = "note",
          Country = "US"
        }
      });

      await store.SaveChangesAsync();

      var results = documents.Search(document =>
        document.Tags.Any(tag => tag == "science") &&
        document.Metadata.Country == "IT");

      Assert.Single(results);
      Assert.Equal("Filtered document", results[0].Value.Title);
      Assert.Equal(1, BitConverter.ToInt32(results[0].Key.Value));

      await store.Close();
    }
    finally
    {
      if (Directory.Exists(basePath))
        Directory.Delete(basePath, true);
    }
  }
}