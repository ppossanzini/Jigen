-- init-pgvector.sql — pgvector initialization for comparative benchmarks
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS embeddings (
    id          TEXT PRIMARY KEY,
    vector      vector(128),
    metadata    JSONB
);

-- IVFFlat index for approximate search (built after data ingestion)
-- Parameters: lists = sqrt(n) ≈ 100 for 10k, 316 for 100k, 1000 for 1M
-- CREATE INDEX ON embeddings USING ivfflat (vector vector_cosine_ops) WITH (lists = 100);

-- For exact search (ground truth), drop the index or use:
-- SET enable_indexscan = OFF;
