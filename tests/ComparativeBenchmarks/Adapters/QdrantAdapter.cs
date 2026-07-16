using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace JigenBenchmarks.Comparative.Adapters;

/// <summary>
/// Qdrant adapter — connects to qdrant:6334 (gRPC) inside Docker,
/// or localhost:6334 when running natively.
/// </summary>
public sealed class QdrantAdapter : IVectorDbAdapter
{
    private QdrantClient? _client;
    private readonly string _host;
    private readonly int _grpcPort;
    private string _collection = "benchmark";
    private int _vectorCount;
    private int _dimension;

    public string Name => "Qdrant";
    public int VectorCount => _vectorCount;

    public QdrantAdapter(string host = "localhost", int grpcPort = 6334)
    {
        _host = host;
        _grpcPort = grpcPort;
    }

    public async Task InitializeAsync(int dimension, CancellationToken ct = default)
    {
        _dimension = dimension;
        _client = new QdrantClient(_host, _grpcPort);

        // Recreate collection
        var collections = await _client.ListCollectionsAsync(cancellationToken: ct);
        if (collections.Any(c => c == _collection))
            await _client.DeleteCollectionAsync(_collection, cancellationToken: ct);

        await _client.CreateCollectionAsync(
            _collection,
            new VectorParams { Size = (ulong)dimension, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default)
    {
        if (_client is null) throw new InvalidOperationException("Call InitializeAsync first.");

        const int batchSize = 500;
        for (int offset = 0; offset < vectors.Length; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = Math.Min(batchSize, vectors.Length - offset);
            var points = new List<PointStruct>(batch);

            for (int i = 0; i < batch; i++)
            {
                var id = Guid.CreateVersion7();
                points.Add(new PointStruct
                {
                    Id = id,
                    Vectors = vectors[offset + i],
                    Payload = { ["idx"] = offset + i }
                });
            }

            await _client.UpsertAsync(_collection, points, cancellationToken: ct);
        }

        _vectorCount += vectors.Length;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        // Qdrant is WAL-based, data is immediately searchable after upsert.
        await Task.CompletedTask;
    }

    public async Task BuildIndexAsync(CancellationToken ct = default)
    {
        // Qdrant builds HNSW incrementally during upsert. Data is already
        // fully searchable; this just ensures any pending background merges settle.
        await Task.Delay(500, ct);
    }

    public async Task<List<(string Id, float Score)>> SearchAsync(
        float[] query, int topK,
        Dictionary<string, object>? filter = null,
        CancellationToken ct = default)
    {
        if (_client is null) throw new InvalidOperationException("Call InitializeAsync first.");

        var results = await _client.SearchAsync(
            _collection,
            query,
            limit: (ulong)topK,
            cancellationToken: ct);

        return results
            .Select(r => (r.Id.ToString(), r.Score))
            .ToList();
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct = default)
    {
        if (_client is null) return;

        var pointIds = ids.Select(id => new PointId { Uuid = id }).ToList();
        await _client.DeleteAsync(_collection, pointIds, cancellationToken: ct);
        _vectorCount = Math.Max(0, _vectorCount - ids.Length);
    }

    public async Task<DbStats> GetStatsAsync(CancellationToken ct = default)
    {
        if (_client is null) return new DbStats(0, 0, 0);

        var info = await _client.GetCollectionInfoAsync(_collection, cancellationToken: ct);
        return new DbStats(
            DiskBytes: 0,
            MemoryBytes: 0,
            VectorCount: _vectorCount);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
