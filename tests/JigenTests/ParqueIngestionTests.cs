using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using JigenTests.entity;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Parquet.Serialization;

namespace JigenTests;

public class ParqueIngestionTests : IDisposable
{
  private Store _store = null;

  public ParqueIngestionTests()
  {
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "openai_llm",
      DataBasePath = "/data/jigendb"
    }, null);
  }

  public void Dispose()
  {
    _store.Dispose();
  }


  [Theory]
  [InlineData("/data/trainingset/train-00000-of-00026-3c7b99d1c7eda36e.parquet")]
  public async Task IngestOneFile(string filename)
  {
    await using Stream fs = File.OpenRead(filename);
    using var reader = await ParquetReader.CreateAsync(fs);

    var schema = reader.Schema.GetDataFields();
    for (int i = 0; i < reader.RowGroupCount; i++)
    {
      using var rg = reader.OpenRowGroupReader(i);

      var id = await rg.ReadColumnAsync(schema[0]);
      var content = await rg.ReadColumnAsync(schema[2]);
      var allembeddings = await rg.ReadColumnAsync(schema[3]);

      int embeddingSize = allembeddings.Data.Length / content.Data.Length;

      for (int j = 0; j < content.Data.Length; j++)
      {
        await _store.AppendContent(new VectorEntry()
        {
          CollectionName = "testone",
          Id = Encoding.UTF8.GetBytes(id.Data.GetValue(j)?.ToString() ?? string.Empty),
          Content = Encoding.UTF8.GetBytes(content.Data.GetValue(j)?.ToString() ?? string.Empty),
          Embedding = ((double?[])allembeddings.Data).Skip(j * embeddingSize).Take(embeddingSize).Select(v => (float)(v ?? 0f)).ToArray()
        });
      }
    }

    await _store.SaveChangesAsync();
  }
}