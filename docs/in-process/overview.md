# In-process engine overview

`Jigen.Store` is the embedded engine at the core of Jigen DB: a vector database that runs inside your own .NET process, with no server to deploy, comparable in spirit to how SQLite embeds a relational database.

## What a Store is

A `Store` (`Jigen.Store` package, targets `net10.0`) represents one database: a named set of files on disk, opened by exactly one process at a time. It owns:

- **Content** — arbitrary document payloads (serialized with MessagePack by default), one per key, grouped into named collections.
- **Embeddings** — an optional `float[]` vector attached to the same key, used for similarity search.
- **A pluggable index** — the component that answers `Search` queries. The default is exact brute-force search; `Jigen.Indexer.HNSW` plugs in an approximate nearest-neighbor index for larger collections. See [Brute-force index](../indexes/brute-force.md) and [HNSW index](../indexes/hnsw.md).

Collections are not declared up front: a collection is simply a name shared by a group of entries, created the first time an entry is written to it.

## Storage model

Each database `{name}` is made of four files, all under `StoreOptions.DataBasePath`:

| File | Purpose |
|---|---|
| `{name}.content.jigen` | Document payloads, append-only |
| `{name}.vectors.jigen` | Embeddings, append-only |
| `{name}.index.jigen` | Position index log (key → offsets), append-only |
| `{name}.lock.jigen` | Exclusivity lock; also doubles as the crash marker |

Content and embeddings are read through memory-mapped files and written through plain `FileStream`s. The position index (key → content/embedding offsets) is rebuilt in memory at startup by replaying the index log, and kept as a `ConcurrentDictionary` per collection for lock-free lookups.

If `Jigen.Indexer.HNSW` is used, it adds its own on-disk graph files per collection — see [HNSW index](../indexes/hnsw.md).

## Writes are asynchronous

`AppendContent` (and `SetContent`, `VectorCollection<T>.Add`, ...) do not write to disk synchronously: the entry is pushed onto an in-memory ingestion queue and a single background writer thread drains it in batches, appending to the content/embeddings/index files. A separate pool of indexing workers (`StoreOptions.IndexerWorkers`) then feeds each entry to the configured index, off the writer's critical path.

This means a `Store` is fast to write to, but "written" and "durable"/"searchable" are different guarantees — see the durability model in [Store options](store-options.md).

## One database, one process

Opening a `Store` acquires an exclusive lock file (`{name}.lock.jigen`). A second `Store` on the same path — in the same or another process — fails to open with an `IOException`. This is a single-writer, embedded design: if you need multiple processes or machines to share a database, run the [server](../server/overview.md) instead and connect with `Jigen.Client`.

## Where to go next

- [Getting started](getting-started.md) — install the package and write a minimal example.
- [Store options](store-options.md) — full `StoreOptions` reference, durability, shrink, crash recovery.
- [Collections](collections.md) — `VectorCollection<T>`, `DocumentCollection<T>`, filtering.
