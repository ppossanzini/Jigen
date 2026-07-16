using Npgsql;

namespace JigenBenchmarks.Comparative.Adapters;

/// <summary>
/// pgvector adapter — connects to PostgreSQL 17 + pgvector.
/// Uses exact search (no IVF index for fair recall comparison).
/// </summary>
public sealed class PgvectorAdapter : IVectorDbAdapter
{
    private readonly string _connectionString;
    private int _vectorCount;
    private int _dimension;

    public string Name => "pgvector";
    public int VectorCount => _vectorCount;

    public PgvectorAdapter(string connectionString = "Host=localhost;Port=5432;Database=vectordb;Username=benchmark;Password=benchmark")
    {
        // Disable pooling + auto-prepare: pgvector casts are incompatible
        // with Npgsql 9.x automatic preparation across connection pool reuse.
        _connectionString = connectionString + ";Pooling=false;Max Auto Prepare=0";
    }

    public async Task InitializeAsync(int dimension, CancellationToken ct = default)
    {
        _dimension = dimension;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Execute each DDL statement separately to avoid prepared-statement conflicts
        await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS embeddings", conn))
            await cmd.ExecuteNonQueryAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            $"CREATE TABLE embeddings (id TEXT PRIMARY KEY, vector vector({dimension}))", conn))
            await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task IngestAsync(
        float[][] vectors,
        Dictionary<string, object>?[]? metadata,
        CancellationToken ct = default)
    {
        // Use text-format COPY for pgvector compatibility with Npgsql 9.x
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var writer = await conn.BeginTextImportAsync(
            "COPY embeddings (id, vector) FROM STDIN (FORMAT TEXT)", ct);

        for (int i = 0; i < vectors.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var id = Guid.CreateVersion7().ToString();
            var vecStr = "[" + string.Join(",", vectors[i].Select(f => f.ToString("F6"))) + "]";
            await writer.WriteAsync($"{id}\t{vecStr}\n".AsMemory(), ct);
        }

        _vectorCount += vectors.Length;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        // Data is immediately visible after COMMIT in PostgreSQL.
        await Task.CompletedTask;
    }

    public async Task<List<(string Id, float Score)>> SearchAsync(
        float[] query, int topK,
        Dictionary<string, object>? filter = null,
        CancellationToken ct = default)
    {
        var connString = _connectionString + ";No Reset On Close=true";
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);

        var vecStr = "[" + string.Join(",", query.Select(f => f.ToString("F6"))) + "]";

        // <=> is cosine distance; 1 - distance = cosine similarity
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, 1 - (vector <=> '{vecStr}'::vector) AS score FROM embeddings ORDER BY vector <=> '{vecStr}'::vector LIMIT {topK}",
            conn);

        var results = new List<(string, float)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add((reader.GetString(0), reader.GetFloat(1)));
        }

        return results;
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM embeddings WHERE id = ANY($1)", conn);
        cmd.Parameters.AddWithValue(ids);
        await cmd.ExecuteNonQueryAsync(ct);
        _vectorCount = Math.Max(0, _vectorCount - ids.Length);
    }

    public async Task<DbStats> GetStatsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT pg_total_relation_size('embeddings'), (SELECT COUNT(*) FROM embeddings)", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return new DbStats(reader.GetInt64(0), 0, reader.GetInt32(1));

        return new DbStats(0, 0, 0);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
