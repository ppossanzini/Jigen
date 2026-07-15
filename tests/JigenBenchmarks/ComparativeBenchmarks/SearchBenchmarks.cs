using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JigenBenchmarks.Comparative.Adapters;
using JigenBenchmarks.Comparative.Datasets;

namespace JigenBenchmarks.Comparative;

/// <summary>
/// BenchmarkDotNet harness for comparative vector DB search benchmarks.
/// Configure via environment variables:
///   BENCH_DB=jigendb|qdrant|milvus|pgvector
///   BENCH_DATASET=random-10k|random-100k|sift-128-euclidean|...
/// </summary>
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 5)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SearchBenchmarks
{
    private IVectorDbAdapter? _adapter;
    private DatasetGenerator.Dataset? _dataset;
    private float[][]? _queries;
    private int _queryIndex;

    [Params(10, 100)]
    public int TopK { get; set; } = 10;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var dbName = Environment.GetEnvironmentVariable("BENCH_DB")
                     ?? throw new InvalidOperationException("BENCH_DB env var not set");
        var datasetName = Environment.GetEnvironmentVariable("BENCH_DATASET")
                          ?? throw new InvalidOperationException("BENCH_DATASET env var not set");

        // Load dataset from shared disk
        _dataset = DatasetGenerator.LoadFromDisk(datasetName);
        _queries = _dataset.TestVectors;

        // Create adapter
        _adapter = CreateAdapter(dbName);
        await _adapter.InitializeAsync(_dataset.Dimension);

        // Ingest all data
        Console.WriteLine($"Ingesting {_dataset.Count} vectors into {dbName}...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _adapter.IngestAsync(_dataset.TrainVectors, _dataset.TrainMetadata);
        await _adapter.FlushAsync();
        Console.WriteLine($"  Done in {sw.Elapsed.TotalSeconds:F1}s ({_dataset.Count / sw.Elapsed.TotalSeconds:N0} vec/s)");
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_adapter != null)
            await _adapter.DisposeAsync();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _queryIndex = 0;
    }

    [Benchmark(Description = "Vector Search")]
    public async Task<List<(string Id, float Score)>> Search()
    {
        var query = _queries![_queryIndex % _queries.Length];
        _queryIndex++;
        return await _adapter!.SearchAsync(query, TopK);
    }

    // ── Factory ──

    private static IVectorDbAdapter CreateAdapter(string name) => name.ToLowerInvariant() switch
    {
        "jigendb" => new JigenAdapter(),
        "qdrant" => new QdrantAdapter(),
        "milvus" => new MilvusAdapter(),
        "pgvector" => new PgvectorAdapter(),
        _ => throw new ArgumentException($"Unknown DB: {name}")
    };
}
