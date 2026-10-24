using System.Diagnostics;
using Jigen;
using Jigen.DataStructures;

var path = Path.Combine(Path.GetFullPath(args[0]), ".temp", $"thresholds-{Guid.CreateVersion7():N}");
Directory.CreateDirectory(path);

try
{
  using var store = new Store(new StoreOptions { DataBaseName = "th", DataBasePath = path });
  var random = new Random(3);
  var corpus = new VectorCollection<Article>(store, new VectorCollectionOptions<Article>
  {
    Name = "docs",
    Dimensions = 2
  });

  const int documents = 30_000;
  for (var document = 0; document < documents; document++)
  {
    var terms = Enumerable.Range(0, 200).OrderBy(_ => random.Next()).Take(8).Order().ToArray()
      .Select(term => new SparseTerm(term, (float)Math.Round(random.NextDouble(), 2))).ToArray();
    await corpus.AddAsync(VectorKey.From(document), new VectorEntry<Article>
    {
      Content = new Article { Title = "d" + document },
      Embeddings = [new float[] { document, 1f }],
      SparseVectors = [terms]
    });
    if (document % 1000 == 999) await store.SaveChangesAsync();
  }
  await store.SaveChangesAsync();
  await store.ShrinkAsync();

  var query = new Dictionary<int, float>
  {
    [3] = 1f, [17] = 0.8f, [42] = 0.6f, [88] = 0.5f, [101] = 0.4f, [150] = 0.3f
  };

  foreach (var k in new[] { 1, 10, 100, 1000, int.MaxValue })
    store.SearchSparse("docs", query, k);

  Console.WriteLine($"{documents} documents, 6 query terms");
  foreach (var k in new[] { 1, 10, 100, 1000, int.MaxValue })
  {
    const int rounds = 20;
    var timer = Stopwatch.StartNew();
    var hits = 0;
    for (var round = 0; round < rounds; round++)
      hits = store.SearchSparse("docs", query, k).Count;
    var ms = timer.Elapsed.TotalMilliseconds / rounds;
    var label = k == int.MaxValue ? "all" : k.ToString();
    Console.WriteLine($"   k={label,-5} {ms,7:F3} ms/query   hits={hits}");
  }
}
finally
{
  if (Directory.Exists(path)) Directory.Delete(path, true);
}

public sealed class Article
{
  public string Title { get; set; } = "";
}
