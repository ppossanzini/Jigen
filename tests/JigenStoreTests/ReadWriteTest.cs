using System.Diagnostics;
using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Xunit.Abstractions;
using ZstdSharp.Unsafe;

namespace JigenTests;

public class ReadWriteTest : IDisposable
{
  private readonly ITestOutputHelper _testOutputHelper;
  private Store _store;
  private Jigen.SemanticTools.OnnxEmbeddingGenerator _embeddingGenerator;

  public ReadWriteTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "readwritetest",
      DataBasePath = "/data/jigendb",
      Indexer = new SmallWorldIndexer(
        new (m : 16, efConstruction : 200, efSearch : 50, storagePath : "/data/jigendb/hnsw"))
    });

    _embeddingGenerator = new(
      "/data/onnx/multi-lingual/tokenizer.onnx",
      "/data/onnx/multi-lingual/model.onnx");
  }

  public void Dispose()
  {
    _store.Dispose();
  }

  (Guid id, string sentence)[] sentences =
  [
    (Guid.NewGuid(), "L'intelligenza artificiale sta trasformando il settore tecnologico."),
    (Guid.NewGuid(), "Il machine learning è una branca fondamentale dell'informatica moderna."),
    (Guid.NewGuid(), "La ricetta della pasta alla carbonara prevede guanciale e uova."),
    (Guid.NewGuid(), "Cucinare gli spaghetti con il guanciale è una tradizione romana."),
    (Guid.NewGuid(), "Il cambiamento climatico rappresenta una minaccia per la biodiversità."),
    (Guid.NewGuid(), "Le emissioni di CO2 contribuiscono al riscaldamento globale."),
    (Guid.NewGuid(), "Il cane corre felice nel parco inseguendo una pallina."),
    (Guid.NewGuid(), "Gli animali domestici richiedono cure e attenzione costante."),
    (Guid.NewGuid(), "La Juventus ha vinto la partita di campionato ieri sera."),
    (Guid.NewGuid(), "Il mondo del calcio è scosso da nuove notizie di mercato."),
    (Guid.NewGuid(), "Sviluppare software richiede logica e molta pazienza."),
    (Guid.NewGuid(), "La programmazione funzionale è un paradigma molto potente.")
  ];

  [Fact]
  public async Task Write()
  {
    foreach (var s in sentences)
      // var s = sentences.First();
    {
      var rr = await _store.AppendContent(new VectorEntry()
      {
        Id = s.id.ToByteArray(),
        CollectionName = "vicini",
        Content = MessagePackDocumentSerializer.Instance.Serialize(s.sentence),
        Embedding = _embeddingGenerator.GenerateEmbedding(s.sentence),
      });

      _testOutputHelper.WriteLine($"{rr.Id} - {rr.Content} - {String.Concat(rr.Embedding.Slice(0, 10).ToArray().Select(i => $"{i},"))}");
    }


    await _store.SaveChangesAsync();
    await _store.Close();
  }

  [Theory]
  [InlineData("animali pelosi")]
  [InlineData("trofei conquistati")]
  [InlineData("viaggiare in lazio")]
  [InlineData("carboidrati")]
  [InlineData("La ricetta della pasta alla carbonara prevede guanciale e uova")]
  public async Task Search(string search)
  {
    var serializer = MessagePackDocumentSerializer.Instance;
    var query = _embeddingGenerator.GenerateEmbedding(search);
    _testOutputHelper.WriteLine($"{String.Concat(query.Take(10).Select(i => $"{i},"))}");

    var sw = new Stopwatch();
    sw.Start();
    var results = _store.Search("vicini", query, 5);
    sw.Stop();

    _testOutputHelper.WriteLine($"Hai cercato: {search} ");
    foreach (var r in results)
    {
      _testOutputHelper.WriteLine($"{new Guid(r.entry.Id)} {serializer.ToJson(r.entry.Content)} {r.score}");
    }

    _testOutputHelper.WriteLine($"Tempo di ricerca: {sw.ElapsedMilliseconds} ms");
    
    await _store.Close();
  }
}