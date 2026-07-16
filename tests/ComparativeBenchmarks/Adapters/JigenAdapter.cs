using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.Indexers;

namespace JigenBenchmarks.Comparative.Adapters;

/// <summary>
/// In-process JigenDb adapter — uses Store + SmallWorldIndexer directly,
/// no network overhead. Best-case baseline for JigenDb performance.
/// </summary>
public sealed class JigenAdapter : IVectorDbAdapter
{
    public enum IndexerMode { Hnsw, BruteForce, LazyHnsw }

    private Store? _store;
    private IIndexer? _indexer;
    private readonly string _storagePath;
    private readonly IndexerMode _mode;
    private readonly int _indexerWorkers;
    private readonly int _lazyThreshold;
    private int _vectorCount;

    public string Name => _mode switch
    {
        IndexerMode.Hnsw => $"JigenDb (HNSW, w={_indexerWorkers})",
        IndexerMode.LazyHnsw => $"JigenDb (LazyHNSW, w={_indexerWorkers}, thr={_lazyThreshold / 1000}k)",
        _ => "JigenDb (BruteForce)"
    };
    public int VectorCount => _vectorCount;

    public JigenAdapter(
        IndexerMode mode = IndexerMode.Hnsw,
        int indexerWorkers = 0,
        string? storagePath = null,
        int lazyThreshold = 9_999)
    {
        _mode = mode;
        // 0 = use default: clamp(cores/2, 1, 8)
        _indexerWorkers = indexerWorkers;
        _storagePath = storagePath ?? Path.Combine(
            Path.GetTempPath(), "jigen-bench", Guid.CreateVersion7().ToString("N"));
        _lazyThreshold = lazyThreshold;
    }

    public Task InitializeAsync(int dimension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storagePath);

        var hnswOpts = new SmallWorldOptions
        {
            M = 16,
            EfConstruction = 200,
            EfSearch = 80,
            StoragePath = Path.Combine(_storagePath, "hnsw")
        };

        IIndexer indexer = _mode switch
        {
            IndexerMode.Hnsw => new SmallWorldIndexer(hnswOpts),
            IndexerMode.LazyHnsw => new LazyIndexer(
                () => new SmallWorldIndexer(hnswOpts),
                _lazyThreshold),
            _ => new BruteForceIndexer()
        };

        var options = new StoreOptions
        {
            DataBasePath = _storagePath,
            DataBaseName = "bench",
            IndexerWorkers = _indexerWorkers > 0
                ? _indexerWorkers
                : Math.Clamp(Environment.ProcessorCount / 2, 1, 8), // default
            Indexer = indexer
        };

        _store = new Store(options);
        _indexer = indexer;
        return Task.CompletedTask;
    }

    public async Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default)
    {
        if (_store is null)
            throw new InvalidOperationException("Call InitializeAsync first.");

        const int batchSize = 4096;
        var bulk = new List<VectorEntry>(batchSize);

        for (int i = 0; i < vectors.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            bulk.Add(new VectorEntry
            {
                Id = Guid.CreateVersion7().ToByteArray(),
                CollectionName = "bench",
                Content = "x"u8.ToArray(),
                Embedding = vectors[i]
            });

            if (bulk.Count >= batchSize || i == vectors.Length - 1)
            {
                await _store.AppendContentBulk(bulk);
                bulk.Clear();
            }
        }

        await _store.SaveChangesAsync();
        _vectorCount += vectors.Length;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_store is not null)
            await _store.SaveChangesAsync();
    }

    public async Task BuildIndexAsync(CancellationToken ct = default)
    {
        if (_store is null || _indexer is null) return;

        // Force reconciliation: for LazyIndexer this triggers the HNSW switch;
        // for HNSW/Brute this is a quick no-op.
        await _indexer.ReconcileAsync(_store);
    }

    public Task<List<(string Id, float Score)>> SearchAsync(
        float[] query, int topK,
        Dictionary<string, object>? filter = null,
        CancellationToken ct = default)
    {
        if (_store is null)
            throw new InvalidOperationException("Call InitializeAsync first.");

        var results = _store.Search("bench", query, topK);
        var list = results
            .Select(r => (Convert.ToHexString(r.entry.Id), r.score))
            .ToList();

        return Task.FromResult(list);
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct = default)
    {
        if (_store is null) return;

        foreach (var idStr in ids)
        {
            // IDs may be GUID strings (from datasets) or hex (from search results).
            byte[] key;
            try { key = Convert.FromHexString(idStr); }
            catch (FormatException) { key = Guid.Parse(idStr).ToByteArray(); }

            await _store.DeleteContent("bench", key);
        }

        await _store.SaveChangesAsync();
        _vectorCount = Math.Max(0, _vectorCount - ids.Length);
    }

    public Task<DbStats> GetStatsAsync(CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(_storagePath);
        long diskBytes = dir.Exists
            ? dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
            : 0;

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        long memBytes = GC.GetTotalMemory(true);

        return Task.FromResult(new DbStats(diskBytes, memBytes, _vectorCount));
    }

    public async ValueTask DisposeAsync()
    {
        if (_store != null)
            await _store.Close();

        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);
    }
}
