using System.Diagnostics;
using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.SemanticTools;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace JigenTests;

[Collection(OnnxModelCollection.Name)]
public class ReadWriteTest : IDisposable
{
  private readonly ITestOutputHelper _testOutputHelper;
  // Each test gets its own database directory: the store holds an exclusive
  // lock per database, and a shared path made concurrent tests fail on it.
  private readonly string _testDirectory;
  private Store _store;
  private Jigen.SemanticTools.OnnxEmbeddingGenerator _embeddingGenerator;

  public ReadWriteTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _testDirectory = Path.Combine(FindRepositoryRoot(), ".temp", nameof(ReadWriteTest),
      Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_testDirectory);
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "readwritetest",
      DataBasePath = _testDirectory,
      Indexer = new SmallWorldIndexer(
        new (m : 16, efConstruction : 200, efSearch : 50, storagePath : Path.Combine(_testDirectory, "hnsw")))
    });

    _embeddingGenerator = new(
      "/data/onnx/multi-lingual/tokenizer.onnx",
      "/data/onnx/multi-lingual/model.onnx",
      null,
      new EmbeddingGeneratorOptions(){});
    
    // _embeddingGenerator = new(
    //   "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx",
    //   "/data/onnx/nomic-embed-text-v1.5/model.onnx",
    //   null,
    //   new EmbeddingGeneratorOptions(){});
  }

  public void Dispose()
  {
    _store.Dispose();
    _embeddingGenerator?.Dispose();
    if (Directory.Exists(_testDirectory))
      Directory.Delete(_testDirectory, recursive: true);
  }

  private static string FindRepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
        return directory.FullName;
      directory = directory.Parent;
    }

    throw new InvalidOperationException("Jigen repository root could not be located.");
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
        Embeddings = [_embeddingGenerator.GenerateEmbedding("search_document", s.sentence)],
      });

      _testOutputHelper.WriteLine($"{rr.Id} - {rr.Content} - {String.Concat(rr.Embeddings[0].Slice(0, Math.Min(10, rr.Embeddings[0].Length)).ToArray().Select(i => $"{i},"))}");
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
    var query = _embeddingGenerator.GenerateEmbedding("search_query",search);
    _testOutputHelper.WriteLine($"{String.Concat(query.Take(10).Select(i => $"{i},"))}");

    var sw = new Stopwatch();
    sw.Start();
    var results = _store.SearchDense("vicini", query, 5);
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