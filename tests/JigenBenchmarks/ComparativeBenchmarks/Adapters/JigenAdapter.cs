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
    private Store? _store;
    private SmallWorldIndexer? _indexer;
    private readonly string _storagePath;
    private int _vectorCount;

    public string Name => "JigenDb (in-process)";
    public int VectorCount => _vectorCount;

    public JigenAdapter(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Path.GetTempPath(), "jigen-bench", Guid.CreateVersion7().ToString("N"));
    }

    public Task InitializeAsync(int dimension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storagePath);

        var options = new StoreOptions
        {
            DataBasePath = _storagePath,
            DataBaseName = "bench",
            Indexer = new SmallWorldIndexer(new SmallWorldOptions
            {
                M = 16,
                EfConstruction = 200,
                EfSearch = 80,
                StoragePath = Path.Combine(_storagePath, "hnsw")
            })
        };

        _store = new Store(options);
        _indexer = (SmallWorldIndexer)options.Indexer;
        return Task.CompletedTask;
    }

    public async Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default)
    {
        if (_store is null)
            throw new InvalidOperationException("Call InitializeAsync first.");

        for (int i = 0; i < vectors.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var id = Guid.CreateVersion7().ToByteArray();
            var entry = new VectorEntry
            {
                Id = id,
                CollectionName = "bench",
                Content = "x"u8.ToArray(),
                Embedding = vectors[i]
            };

            await _store.AppendContent(entry);
        }

        await _store.SaveChangesAsync();
        _vectorCount += vectors.Length;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_store is not null)
            await _store.SaveChangesAsync();
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
