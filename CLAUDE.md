# Jigen — notes for Claude

Embedded/server vector database in .NET: `Store` (append-only content+embedding files), pluggable indexers (brute-force, HNSW), gRPC/REST server, typed client. Docs live in `docs/` (mkdocs).

## Layout

- `src/Jigen/Jigen.Indexer.HNSW/` — HNSW index (`SmallWorldIndexer`), mmap zero-copy vectors, SQ8 quantization.
- `src/Jigen/Jigen.Store/` — store, brute-force indexer, filtering glue.
- `src/Jigen/Jigen.Primitives/` — shared contracts (`IStore`, `IIndexer`, filter AST, `MessagePackFilterEvaluator`).
- `src/Server/` — gRPC/REST server and handlers. `src/Client/` — .NET client.
- `tests/HnswTest/`, `tests/JigenStoreTests/` — main suites. `tests/JigenBenchmarks/` — BenchmarkDotNet.

## Build & test quirks

- `Jigen.Client` and `Jigen.Grpc` fail to build on Apple Silicon: grpc.tools ships an x64 `protoc` ("Bad CPU type"). Build the other projects individually.
- In `tests/JigenStoreTests`, `IngestionTests` and `ReadWriteTest` use hardcoded `/data/jigendb` paths and fail in the constructor on machines without that directory. Pre-existing; not a regression signal.

## HNSW index — decisions to preserve

- **Filtered search is ACORN-1 style** (since 2026-07-12): metadata filters are evaluated *during* level-0 traversal via the `accept` predicate in `KNearestAtLevel`; rejected nodes stay navigable (like deleted ones) but never enter the result window. Expansion is bounded by `SmallWorldOptions.FilteredSearchExpansionFactor` (default 20 × ef). Test: `tests/HnswTest/FilteredSearchTest.cs`. Do not revert to post-filtering a fixed candidate window.
- **`SmallWorldOptions.NodeCacheSize` is a compat-only shim**, deliberately not wired to anything (nodes are RAM-resident by design; vectors come from the mmap). Don't remove it and don't "wire it up" — see `docs/indexes/hnsw.md`.
- **`SmallWorldOptions.generator` defaults to `Random.Shared`** so level assignment is lock-free; a custom (seeded) `Random` re-enables the lock in `GetMaxLevel`. Tests rely on seeded generators for determinism.
- **Remap cadence is amortized**: `SplitNodeList.CurrentStageLimit` grows with the graph (4096→65536) because every `MappedVectorFile.Remap()` permanently retains the retired view until dispose. Don't go back to a fixed small cadence.
- Lock order everywhere in the indexer: `graph.nodes` → node → storage. Never node → graph.
