using Milvus.Client;

namespace JigenBenchmarks.Comparative.Adapters;

/// <summary>
/// Milvus adapter — connects via gRPC (localhost:19530).
/// Uses COSINE metric, varchar primary keys, IVF_FLAT index.
/// </summary>
public sealed class MilvusAdapter : IVectorDbAdapter
{
    private MilvusClient? _client;
    private MilvusCollection? _collection;
    private readonly string _host;
    private readonly int _port;
    private const string CollectionName = "benchmark";
    private int _vectorCount;
    private int _dimension;
    private readonly List<string> _ids = new();

    public string Name => "Milvus";
    public int VectorCount => _vectorCount;

    public MilvusAdapter(string host = "localhost", int port = 19530)
    {
        _host = host;
        _port = port;
    }

    public async Task InitializeAsync(int dimension, CancellationToken ct = default)
    {
        _dimension = dimension;
        _client = new MilvusClient(_host, _port);

        var hasCollection = await _client.HasCollectionAsync(CollectionName, cancellationToken: ct);
        if (hasCollection)
        {
            var oldColl = _client.GetCollection(CollectionName);
            await oldColl.DropAsync(ct);
        }

        var fields = new FieldSchema[]
        {
            FieldSchema.CreateVarchar("id", 64, isPrimaryKey: true, autoId: false, isPartitionKey: false, "primary id"),
            FieldSchema.CreateFloatVector("vector", dimension, "embedding vector"),
        };

        _collection = await _client.CreateCollectionAsync(CollectionName, fields, cancellationToken: ct);

        await _collection.CreateIndexAsync(
            "vector", IndexType.IvfFlat, SimilarityMetricType.Cosine,
            extraParams: new Dictionary<string, string> { ["nlist"] = "128" },
            cancellationToken: ct);

        await _collection.LoadAsync(cancellationToken: ct);
    }

    public async Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default)
    {
        if (_collection is null) throw new InvalidOperationException("Call InitializeAsync first.");

        const int batchSize = 1000;
        for (int offset = 0; offset < vectors.Length; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = Math.Min(batchSize, vectors.Length - offset);

            var ids = new List<string>(batch);
            var floatVecs = new List<ReadOnlyMemory<float>>(batch);

            for (int i = 0; i < batch; i++)
            {
                ids.Add(Guid.CreateVersion7().ToString());
                floatVecs.Add(new ReadOnlyMemory<float>(vectors[offset + i]));
            }

            var fieldData = new FieldData[]
            {
                FieldData.CreateVarChar("id", ids, isDynamic: false),
                FieldData.CreateFloatVector("vector", floatVecs),
            };

            await _collection.InsertAsync(fieldData, cancellationToken: ct);
            _ids.AddRange(ids);
        }

        _vectorCount += vectors.Length;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_client is null || _collection is null) return;
        await _client.FlushAsync(new[] { CollectionName }, ct);
        await _collection.LoadAsync(cancellationToken: ct);
    }

    public async Task<List<(string Id, float Score)>> SearchAsync(
        float[] query, int topK,
        Dictionary<string, object>? filter = null,
        CancellationToken ct = default)
    {
        if (_collection is null) throw new InvalidOperationException("Call InitializeAsync first.");

        var parameters = new SearchParameters();
        parameters.ExtraParameters["nprobe"] = "16";

        var results = await _collection.SearchAsync(
            "vector",
            new ReadOnlyMemory<float>[] { new ReadOnlyMemory<float>(query) },
            SimilarityMetricType.Cosine,
            topK,
            parameters,
            ct);

        var ids = results.Ids.StringIds!;
        var scores = results.Scores;

        var list = new List<(string, float)>(Math.Min(ids.Count, scores.Count));
        for (int i = 0; i < ids.Count && i < scores.Count; i++)
            list.Add((ids[i], scores[i]));

        return list;
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct = default)
    {
        if (_collection is null) return;
        var expr = $"id in [{string.Join(",", ids.Select(i => $"\"{i}\""))}]";
        await _collection.DeleteAsync(expr, cancellationToken: ct);
        _vectorCount = Math.Max(0, _vectorCount - ids.Length);
    }

    public async Task<DbStats> GetStatsAsync(CancellationToken ct = default)
    {
        if (_collection is null) return new DbStats(0, 0, 0);
        var count = await _collection.GetEntityCountAsync(ct);
        return new DbStats(0, 0, (int)count);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
