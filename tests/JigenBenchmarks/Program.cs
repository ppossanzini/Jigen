// Jigen macro-benchmark: ingest / search (with recall) / delete / reopen on a
// disk-backed store with the HNSW indexer.
//
//   dotnet run -c Release [-- N [dim] [sq8]]      (defaults: 10000 128)
//
// Run it before and after performance work: the recall column is the guard
// against "faster but wrong" regressions.

using System.Diagnostics;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Indexers;

var n = args.Length > 0 && int.TryParse(args[0], out var argN) ? argN : 10_000;
var dim = args.Length > 1 && int.TryParse(args[1], out var argD) ? argD : 128;
var sq8 = args.Contains("sq8", StringComparer.OrdinalIgnoreCase);
var workersArg = args.FirstOrDefault(a => a.StartsWith("w") && int.TryParse(a.AsSpan(1), out _));
var workers = workersArg is null ? (int?)null : int.Parse(workersArg.AsSpan(1));

const int SearchQueries = 500;
const int RecallQueries = 100;
const int DeleteCount = 500;
const int Top = 10;

var root = Path.Combine(Path.GetTempPath(), "jigen-bench", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

StoreOptions OptionsFor() => new()
{
  DataBasePath = root,
  DataBaseName = "bench",
  IndexerWorkers = workers ?? Math.Clamp(Environment.ProcessorCount / 2, 1, 8),
  Indexer = new SmallWorldIndexer(new SmallWorldOptions
  {
    M = 16,
    EfConstruction = 200,
    EfSearch = 80,
    StoragePath = Path.Combine(root, "hnsw"),
    generator = new Random(4242),
    Quantization = sq8 ? VectorQuantization.SQ8 : VectorQuantization.None
  })
};

Console.WriteLine($"Jigen bench — N={n}, dim={dim}, M=16, efC=200, efS=80, quant={(sq8 ? "SQ8" : "none")}, workers={workers?.ToString() ?? "auto"}");
Console.WriteLine(new string('-', 64));

try
{
  var store = new Store(OptionsFor());
  var random = new Random(42);
  var vectors = new float[n][];
  var ids = new byte[n][];
  for (var i = 0; i < n; i++)
  {
    vectors[i] = CreateUnitVector(random, dim);
    ids[i] = Guid.NewGuid().ToByteArray();
  }

  // ---- ingest -------------------------------------------------------------
  var sw = Stopwatch.StartNew();
  for (var i = 0; i < n; i++)
    await store.AppendContent(new VectorEntry
    {
      Id = ids[i], CollectionName = "bench", Content = "x"u8.ToArray(), Embedding = vectors[i]
    });
  await store.SaveChangesAsync();
  sw.Stop();
  Report("ingest", $"{n / sw.Elapsed.TotalSeconds,10:0} vec/s   ({sw.Elapsed.TotalSeconds:0.0}s total)");

  // ---- HNSW search + recall vs brute force --------------------------------
  var brute = new BruteForceIndexer();

  sw.Restart();
  for (var i = 0; i < SearchQueries; i++)
    _ = store.Search("bench", vectors[(int)((long)i * n / SearchQueries)], Top).ToList();
  sw.Stop();
  Report("hnsw search", $"{sw.Elapsed.TotalMilliseconds * 1000 / SearchQueries,10:0} us/query");

  var recallSum = 0d;
  for (var i = 0; i < RecallQueries; i++)
  {
    var query = vectors[(int)((long)i * n / RecallQueries)];
    var expected = brute.Search(store, "bench", query, Top)
      .Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
    var got = store.Search("bench", query, Top)
      .Select(r => Convert.ToBase64String(r.entry.Id)).ToHashSet(StringComparer.Ordinal);
    recallSum += (double)got.Intersect(expected, StringComparer.Ordinal).Count() / Top;
  }
  Report($"recall@{Top}", $"{recallSum / RecallQueries,10:0.000}");

  sw.Restart();
  for (var i = 0; i < 100; i++)
    _ = brute.Search(store, "bench", vectors[(int)((long)i * n / 100)], Top).ToList();
  sw.Stop();
  Report("brute force", $"{sw.Elapsed.TotalMilliseconds * 1000 / 100,10:0} us/query (exact)");

  // ---- delete -------------------------------------------------------------
  sw.Restart();
  for (var i = 0; i < DeleteCount; i++)
    await store.DeleteContent("bench", ids[i]);
  sw.Stop();
  Report("delete", $"{sw.Elapsed.TotalMilliseconds * 1000 / DeleteCount,10:0} us/delete");
  await store.SaveChangesAsync();

  // ---- memory -------------------------------------------------------------
  GC.Collect();
  GC.WaitForPendingFinalizers();
  GC.Collect();
  Report("managed heap", $"{GC.GetTotalMemory(true) / 1024.0 / 1024.0,10:0.0} MB");

  // ---- reopen -------------------------------------------------------------
  await store.Close();
  sw.Restart();
  store = new Store(OptionsFor());
  _ = store.Search("bench", vectors[n / 2], Top).ToList();
  sw.Stop();
  Report("reopen+query", $"{sw.Elapsed.TotalMilliseconds,10:0} ms");

  // ---- disk ---------------------------------------------------------------
  var graphBytes = Directory.GetFiles(Path.Combine(root, "hnsw")).Sum(f => new FileInfo(f).Length);
  var rawBytes = (long)n * dim * sizeof(float);
  Report("graph on disk", $"{graphBytes / 1024.0 / 1024.0,10:0.00} MB (raw vectors {rawBytes / 1024.0 / 1024.0:0.00} MB)");

  await store.Close();
}
finally
{
  Directory.Delete(root, recursive: true);
}

return;

static void Report(string label, string value) => Console.WriteLine($"{label,-14}{value}");

static float[] CreateUnitVector(Random random, int dimensions)
{
  var vector = new float[dimensions];
  var norm = 0f;

  for (var i = 0; i < dimensions; i++)
  {
    var value = (float)(random.NextDouble() * 2.0 - 1.0);
    vector[i] = value;
    norm += value * value;
  }

  if (norm <= 0f) return vector;

  var invNorm = 1f / MathF.Sqrt(norm);
  for (var i = 0; i < dimensions; i++)
    vector[i] *= invNorm;

  return vector;
}
