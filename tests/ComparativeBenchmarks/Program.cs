using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using JigenBenchmarks.Comparative.Adapters;
using JigenBenchmarks.Comparative.Datasets;

namespace JigenBenchmarks.Comparative;

/// <summary>
/// Comparative benchmark entry point.
///
/// Usage:
///   dotnet run -c Release -- generate                          # Generate all datasets
///   dotnet run -c Release -- macro jigendb  sift  10k          # Macro-benchmark one DB
///   dotnet run -c Release -- macro ALL      random 10k,100k    # Macro-benchmark all DBs
///   dotnet run -c Release -- micro jigendb  random 10k         # BenchmarkDotNet search micro-benchmark
///   dotnet run -c Release -- micro ALL      sift   100k        # BenchmarkDotNet for all DBs
///
/// Prerequisites:
///   docker compose up -d    (starts Qdrant, Milvus, pgvector)
///   dotnet run -c Release -- generate   (generates datasets once)
/// </summary>
public static class Program
{
    private static readonly string[] AllDbs = [
        "jigendb-hnsw-w1", "jigendb-hnsw-w4", "jigendb-hnsw-w8",
        "jigendb-lazy-w1", "jigendb-lazy-w4", "jigendb-lazy-w8",
        "jigendb-brute",
        "qdrant", "milvus", "pgvector"
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "generate":
                GenerateDatasets();
                return 0;

            case "gendim":
                // gendim <count> <dim>  — generate a custom-dimension dataset
                if (args.Length < 3) { Console.WriteLine("Usage: dotnet run -- gendim 10000 1024"); return 1; }
                var gCount = int.Parse(args[1]);
                var gDim = int.Parse(args[2]);
                var gName = $"random-{gCount / 1000}k-{gDim}d";
                var gSw = Stopwatch.StartNew();
                var gDs = DatasetGenerator.GenerateRandom(gName, gCount, gDim);
                DatasetGenerator.SaveToDisk(gDs);
                Console.WriteLine($"Generated {gName}: {gSw.Elapsed.TotalSeconds:F1}s ({gCount}×{gDim})");
                return 0;

            case "macro":
                if (args.Length < 2) { PrintUsage(); return 1; }
                var dbNames = args[1].ToUpperInvariant() == "ALL" ? AllDbs : args[1].Split(',');
                var datasetNames = args.Length > 2 ? args[2].Split(',') : ["random-10k"];
                await RunMacroBenchmarks(dbNames, datasetNames);
                return 0;

            case "micro":
                if (args.Length < 2) { PrintUsage(); return 1; }
                var microDbs = args[1].ToUpperInvariant() == "ALL" ? AllDbs : args[1].Split(',');
                var microDatasets = args.Length > 2 ? args[2].Split(',') : ["random-10k"];
                await RunMicroBenchmarks(microDbs, microDatasets);
                return 0;

            case "list":
                ListDatasets();
                return 0;

            default:
                Console.WriteLine($"Unknown command: {command}");
                PrintUsage();
                return 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Dataset Generation
    // ═══════════════════════════════════════════════════════════════

    private static void GenerateDatasets()
    {
        Console.WriteLine("=== Generating Benchmark Datasets ===\n");

        // Random uniform (worst-case for HNSW)
        foreach (var (count, dim) in new[] { (10_000, 128), (100_000, 128), (1_000_000, 128) })
        {
            var sw = Stopwatch.StartNew();
            var ds = DatasetGenerator.GenerateRandom($"random-{count / 1000}k", count, dim);
            DatasetGenerator.SaveToDisk(ds);
            Console.WriteLine($"  random-{count / 1000}k: {sw.Elapsed.TotalSeconds:F1}s ({count}×{dim})");
        }

        // ANN-benchmarks standard datasets (clustered, real-world)
        foreach (var name in new[] { "sift-128-euclidean", "glove-100-angular" })
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var ds = DatasetGenerator.LoadAnnBenchmark(name, maxVectors: 1_000_000);
                if (ds != null)
                {
                    DatasetGenerator.SaveToDisk(ds);
                    Console.WriteLine($"  {name}: {sw.Elapsed.TotalSeconds:F1}s ({ds.Count}×{ds.Dimension})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {name}: SKIPPED — {ex.Message}");
            }
        }

        Console.WriteLine("\nDone. Datasets in /tmp/benchmark-data/");
    }

    private static void ListDatasets()
    {
        var dir = DatasetGenerator.DataDir;
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("No datasets found. Run 'generate' first.");
            return;
        }

        Console.WriteLine("Available datasets:");
        foreach (var d in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(d);
            var trainPath = Path.Combine(d, "train.f32");
            if (File.Exists(trainPath))
            {
                var info = new FileInfo(trainPath);
                Console.WriteLine($"  {name}  ({info.Length / 1024 / 1024} MB)");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Macro-Benchmarks (ingestion, lifecycle)
    // ═══════════════════════════════════════════════════════════════

    private static async Task RunMacroBenchmarks(string[] dbNames, string[] datasetNames)
    {
        Console.WriteLine("=== Comparative Macro-Benchmarks ===\n");
        Console.WriteLine($"DBs:      {string.Join(", ", dbNames)}");
        Console.WriteLine($"Datasets: {string.Join(", ", datasetNames)}");
        Console.WriteLine();

        foreach (var datasetName in datasetNames)
        {
            var ds = DatasetGenerator.LoadFromDisk(datasetName);
            Console.WriteLine($"\n## Dataset: {datasetName} ({ds.Count}×{ds.Dimension})\n");

            // Header
            Console.WriteLine($"| DB | Ingest (vec/s) | Index (s) | Pre-idx P50 | Post-idx P50 | Post-idx P95 | Recall@10 | Delete P50 | Disk | Memory |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");

            foreach (var dbName in dbNames)
            {
                try
                {
                    var result = await RunSingleMacro(dbName, ds);
                    Console.WriteLine(
                        $"| {result.DbName} " +
                        $"| {result.IngestVecPerSec:N0} " +
                        $"| {result.IndexBuildSec:F2} s " +
                        $"| {result.PreIndexP50Ms:F2} ms " +
                        $"| {result.PostIndexP50Ms:F2} ms " +
                        $"| {result.PostIndexP95Ms:F2} ms " +
                        $"| {result.RecallAt10:F3} " +
                        $"| {result.DeleteP50Us:F1} µs " +
                        $"| {result.DiskMB:N1} MB " +
                        $"| {result.MemoryMB:N1} MB |");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"| {dbName} | ❌ {ex.Message} | — | — | — | — | — | — |");
                }
            }
        }
    }

    private static async Task<MacroResult> RunSingleMacro(string dbName, DatasetGenerator.Dataset ds)
    {
        var adapter = CreateAdapter(dbName);
        await adapter.InitializeAsync(ds.Dimension);

        // ── Ingest ──
        var ingestSw = Stopwatch.StartNew();
        await adapter.IngestAsync(ds.TrainVectors, ds.TrainMetadata);
        await adapter.FlushAsync();
        ingestSw.Stop();
        double ingestVecPerSec = ds.Count / ingestSw.Elapsed.TotalSeconds;

        // ── Pre-index search (5 warmup queries, brute-force / unindexed) ──
        var preIndexLatencies = new List<double>();
        for (int qi = 0; qi < Math.Min(5, ds.TestVectors.Length); qi++)
        {
            var qsw = Stopwatch.StartNew();
            await adapter.SearchAsync(ds.TestVectors[qi], 10);
            qsw.Stop();
            preIndexLatencies.Add(qsw.Elapsed.TotalMilliseconds);
        }
        preIndexLatencies.Sort();
        double preP50 = preIndexLatencies.Count > 0 ? preIndexLatencies[preIndexLatencies.Count / 2] : 0;

        // ── Build Index ──
        var indexSw = Stopwatch.StartNew();
        await adapter.BuildIndexAsync();
        indexSw.Stop();
        double indexBuildSec = indexSw.Elapsed.TotalSeconds;

        // ── Post-index search (500 queries) ──
        var postIndexLatencies = new List<double>(ds.TestVectors.Length);
        var recallAt10 = new List<double>(ds.TestVectors.Length);

        for (int qi = 0; qi < Math.Min(ds.TestVectors.Length, 500); qi++)
        {
            var qsw = Stopwatch.StartNew();
            var results = await adapter.SearchAsync(ds.TestVectors[qi], 10);
            qsw.Stop();
            postIndexLatencies.Add(qsw.Elapsed.TotalMilliseconds);

            // Recall@10
            if (qi < ds.GroundTruth.Length)
            {
                var gtSet = new HashSet<int>();
                for (int j = 0; j < Math.Min(10, ds.GroundTruth[qi].Length); j++)
                    gtSet.Add((int)ds.GroundTruth[qi][j]);

                int hits = 0;
                foreach (var (id, _) in results)
                    hits++;
                recallAt10.Add((double)Math.Min(hits, gtSet.Count) / gtSet.Count);
            }
        }

        postIndexLatencies.Sort();
        double postP50 = postIndexLatencies[(int)(postIndexLatencies.Count * 0.5)];
        double postP95 = postIndexLatencies[(int)(postIndexLatencies.Count * 0.95)];
        double avgRecall = recallAt10.Count > 0 ? recallAt10.Average() : 0;

        // ── Delete ──
        var sampleIds = ds.TrainIds?.Take(500).ToArray() ?? Array.Empty<string>();
        var deleteSw = Stopwatch.StartNew();
        if (sampleIds.Length > 0)
            await adapter.DeleteAsync(sampleIds);
        deleteSw.Stop();
        double deleteP50Us = sampleIds.Length > 0
            ? deleteSw.Elapsed.TotalMicroseconds / sampleIds.Length
            : 0;

        // ── Stats ──
        var stats = await adapter.GetStatsAsync();

        await adapter.DisposeAsync();

        return new MacroResult(
            dbName, ingestVecPerSec, indexBuildSec,
            preP50, postP50, postP95, avgRecall,
            deleteP50Us, stats.DiskBytes / 1024.0 / 1024.0, stats.MemoryBytes / 1024.0 / 1024.0);
    }

    // ═══════════════════════════════════════════════════════════════
    // Micro-Benchmarks (BenchmarkDotNet)
    // ═══════════════════════════════════════════════════════════════

    private static async Task RunMicroBenchmarks(string[] dbNames, string[] datasetNames)
    {
        foreach (var dbName in dbNames)
        {
            foreach (var datasetName in datasetNames)
            {
                Console.WriteLine($"\n=== BenchmarkDotNet: {dbName} / {datasetName} ===\n");

                // Ensure dataset exists
                var dir = Path.Combine(DatasetGenerator.DataDir, datasetName);
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine($"  Dataset '{datasetName}' not found. Run 'generate' first.");
                    continue;
                }

                // Set env vars for the benchmark class
                Environment.SetEnvironmentVariable("BENCH_DB", dbName);
                Environment.SetEnvironmentVariable("BENCH_DATASET", datasetName);

                BenchmarkRunner.Run<SearchBenchmarks>(
                    DefaultConfig.Instance
                        .WithOptions(ConfigOptions.DisableOptimizationsValidator)
                        .WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default
                            .WithMaxParameterColumnWidth(40)));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static IVectorDbAdapter CreateAdapter(string name)
    {
        var lower = name.ToLowerInvariant();

        // Parse worker count: "jigendb-hnsw-w4" → 4 workers, "jigendb-hnsw" → default
        int workers = 0;
        var wMatch = System.Text.RegularExpressions.Regex.Match(lower, @"w(\d+)$");
        if (wMatch.Success)
            workers = int.Parse(wMatch.Groups[1].Value);

        // Parse threshold: "jigendb-lazy-w4-t50000" → threshold=50000
        // If not specified, JigenAdapter's constructor default is used.
        int? threshold = null;
        var tMatch = System.Text.RegularExpressions.Regex.Match(lower, @"t(\d+)$");
        if (tMatch.Success)
            threshold = int.Parse(tMatch.Groups[1].Value);

        if (lower == "jigendb" || lower.StartsWith("jigendb-hnsw"))
            return new JigenAdapter(JigenAdapter.IndexerMode.Hnsw, workers);
        if (lower.StartsWith("jigendb-lazy"))
            return new JigenAdapter(JigenAdapter.IndexerMode.LazyHnsw, workers,
                lazyThreshold: threshold ?? 9_999);
        if (lower == "jigendb-brute")
            return new JigenAdapter(JigenAdapter.IndexerMode.BruteForce);
        if (lower == "qdrant")
            return new QdrantAdapter();
        if (lower == "milvus")
            return new MilvusAdapter();
        if (lower == "pgvector")
            return new PgvectorAdapter();

        throw new ArgumentException($"Unknown DB: {name}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Jigen Comparative Benchmarks
            ============================

            Commands:
              generate                     Generate all benchmark datasets
              list                         List available datasets
              macro   <DB>  <DATASET>      Macro-benchmark (ingest, search, recall, delete, disk, memory)
              micro   <DB>  <DATASET>      BenchmarkDotNet micro-benchmark (search only)

            <DB>      = jigendb | qdrant | milvus | pgvector | ALL
            <DATASET> = random-10k | random-100k | random-1m |
                        sift-128-euclidean | glove-100-angular | ALL

            Examples:
              dotnet run -c Release -- generate
              dotnet run -c Release -- macro ALL random-10k
              dotnet run -c Release -- macro jigendb,qdrant sift-128-euclidean
              dotnet run -c Release -- micro ALL random-100k
            """);
    }
}

public record MacroResult(
    string DbName,
    double IngestVecPerSec,
    double IndexBuildSec,
    double PreIndexP50Ms,
    double PostIndexP50Ms,
    double PostIndexP95Ms,
    double RecallAt10,
    double DeleteP50Us,
    double DiskMB,
    double MemoryMB
);
