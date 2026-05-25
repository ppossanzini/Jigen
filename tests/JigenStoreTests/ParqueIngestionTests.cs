using System.Diagnostics;
using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using JigenTests.entity;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Parquet.Serialization;
using Xunit.Abstractions;

namespace JigenTests;

public class ParqueIngestionTests : IDisposable
{
  private readonly ITestOutputHelper _testOutputHelper;
  private Store _store = null;
  private Jigen.SemanticTools.OnnxEmbeddingGenerator _embeddingGenerator;

  public ParqueIngestionTests(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "openai_llm",
      DataBasePath = "/data/jigendb", 
      Indexer = new SmallWorldIndexer(new SmallWorldOptions(
        m:16,
        efConstruction:200,
        efSearch:50, storagePath: "/data/jigendb/hnsw"){InMemory = false})
    });
    
    _embeddingGenerator = new(
      "/data/onnx/multi-lingual/tokenizer.onnx",
      "/data/onnx/multi-lingual/model.onnx");
  }

  public void Dispose()
  {
    _store.Dispose();
  }

  [Theory]
  [InlineData("Libri illustrati")]
  [InlineData("attori nello spazio")]
  public async Task Search(string search)
  {
    var serializer = MessagePackDocumentSerializer.Instance;
    var query = _embeddingGenerator.GenerateEmbedding(search);
    _testOutputHelper.WriteLine($"{String.Concat(query.Take(10).Select(i => $"{i},"))}");

    var sw = new Stopwatch();
    sw.Start();
    var results = _store.Search("testone", query, 5);
    sw.Stop();

    _testOutputHelper.WriteLine($"Hai cercato: {search} ");
    foreach (var r in results)
    {
      _testOutputHelper.WriteLine($"{Encoding.UTF8.GetString(r.entry.Id)} {serializer.ToJson(r.entry.Content)} {r.score}");
    }

    _testOutputHelper.WriteLine($"Tempo di ricerca: {sw.ElapsedMilliseconds} ms");
    
    await _store.Close();
    
  }
  
  [Theory]
  [InlineData("/data/trainingset/train-00000-of-00026-3c7b99d1c7eda36e.parquet")]
  public async Task IngestOneFile(string filename)
  {
    await using Stream fs = File.OpenRead(filename);
    using var reader = await ParquetReader.CreateAsync(fs);

    var schema = reader.Schema.GetDataFields();
    var count = 0;
    for (int i = 0; i < reader.RowGroupCount; i++)
    {
      using var rg = reader.OpenRowGroupReader(i);

      var id = await rg.ReadColumnAsync(schema[0]);
      var content = await rg.ReadColumnAsync(schema[2]);
      var allembeddings = await rg.ReadColumnAsync(schema[3]);

      int embeddingSize = allembeddings.Data.Length / content.Data.Length;

      for (int j = 0; j < content.Data.Length; j++)
      {
        if(count++ > 100) break;
        _testOutputHelper.WriteLine($"Ingesting {count}");
        await _store.AppendContent(new VectorEntry()
        {
          CollectionName = "testone",
          Id = Encoding.UTF8.GetBytes(id.Data.GetValue(j)?.ToString() ?? string.Empty),
          Content = Encoding.UTF8.GetBytes(content.Data.GetValue(j)?.ToString() ?? string.Empty),
          Embedding = _embeddingGenerator.GenerateEmbedding(content.Data.GetValue(j)?.ToString() ?? string.Empty),
        });
      }
    }

    await _store.SaveChangesAsync();
    await _store.Close();
    // await _store.Options.Indexer.ShrinkAsync();
  }
}