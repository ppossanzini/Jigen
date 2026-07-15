namespace JigenBenchmarks.Comparative;

/// <summary>
/// Common interface that every vector DB adapter must implement.
/// </summary>
public interface IVectorDbAdapter : IAsyncDisposable
{
    /// <summary>Human-readable name for reports (e.g. "JigenDb", "Qdrant").</summary>
    string Name { get; }

    /// <summary>One-time setup: create collection/index with given dimension.</summary>
    Task InitializeAsync(int dimension, CancellationToken ct = default);

    /// <summary>Ingest a batch of vectors with optional metadata.</summary>
    Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default);

    /// <summary>Flush / ensure all ingested data is searchable.</summary>
    Task FlushAsync(CancellationToken ct = default);

    /// <summary>Search top-K nearest neighbours. Returns (id, score) tuples.</summary>
    Task<List<(string Id, float Score)>> SearchAsync(
        float[] query, int topK,
        Dictionary<string, object>? filter = null,
        CancellationToken ct = default);

    /// <summary>Delete vectors by id.</summary>
    Task DeleteAsync(string[] ids, CancellationToken ct = default);

    /// <summary>Get stats about the current state.</summary>
    Task<DbStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>Total number of vectors currently stored.</summary>
    int VectorCount { get; }
}

public record DbStats(
    long DiskBytes,
    long MemoryBytes,
    int VectorCount
);
