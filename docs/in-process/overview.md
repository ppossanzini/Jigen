# In-process engine overview

`Jigen.Store` is the embedded engine at the core of Jigen DB: a vector database that runs inside your own .NET process, with no server to deploy, comparable in spirit to how SQLite embeds a relational database.

## What a Store is

A `Store` (`Jigen.Store` package, targets `net10.0`) represents one database: a named set of files on disk, opened by exactly one process at a time. It owns:

- **Content** — arbitrary document payloads (serialized with MessagePack by default), one per key, grouped into named collections.
- **Embeddings** — an optional `float[]` vector attached to the same key, used for similarity search.
- **A pluggable index** — the component that answers `Search` queries. The default is exact brute-force search; `Jigen.Indexer.HNSW` plugs in an approximate nearest-neighbor index for larger collections. See [Brute-force index](../indexes/brute-force.md) and [HNSW index](../indexes/hnsw.md).

Collections are not declared up front: a collection is simply a name shared by a group of entries, created the first time an entry is written to it.

## Storage model

Each database `{name}` is made of five files, all under `StoreOptions.DataBasePath`:

| File | Purpose |
|---|---|
| `{name}.content.jigen` | Document payloads, append-only |
| `{name}.vectors.jigen` | Embeddings, append-only |
| `{name}.index.jigen` | Position index log (key → offsets), append-only |
| `{name}.lock.jigen` | Exclusivity lock; also doubles as the crash marker |
| `{name}.wal.jigen` | **Optional** Write-Ahead Log: CRC32-protected, written before the ingestion queue for per-record durability (see [Store options](store-options.md#write-ahead-log-wal)) |

Content and embeddings are read through memory-mapped files and written through plain `FileStream`s. The position index (key → content/embedding offsets) is rebuilt in memory at startup by replaying the index log, and kept as a `ConcurrentDictionary` per collection for lock-free lookups.

If `Jigen.Indexer.HNSW` is used, it adds its own on-disk graph files per collection — see [HNSW index](../indexes/hnsw.md).

## Writes are asynchronous

`AppendContent` (and `SetContent`, `VectorCollection<T>.Add`, ...) do not write to disk synchronously: the entry is pushed onto an in-memory ingestion queue and a single background writer thread drains it in batches, appending to the content/embeddings/index files. A separate pool of indexing workers (`StoreOptions.IndexerWorkers`) then feeds each entry to the configured index, off the writer's critical path.

When the [Write-Ahead Log](store-options.md#write-ahead-log-wal) is enabled (`WalOptions.Enabled = true`), `AppendContent` writes to the WAL file **before** enqueuing to the ingestion queue, providing per-record durability without changing the writer thread.

This means a `Store` is fast to write to, but "written" and "durable"/"searchable" are different guarantees — see the durability model in [Store options](store-options.md).

## Atomic transactions

When the WAL is enabled, `Store.BeginTransaction()` creates a multi-entry transaction. Operations are buffered in memory and become atomically durable when `CommitAsync()` is called: the entire transaction is written to the WAL as a single `[BEGIN][ops…][COMMIT]` block.

```csharp
using var tx = store.BeginTransaction();
tx.Append(new VectorEntry { Id = key1, CollectionName = "docs", … });
tx.Delete("docs", key2);
await tx.CommitAsync();
```

If the transaction is not committed (rolled back, or disposed without calling `CommitAsync`), no data reaches the WAL and nothing is persisted. On crash recovery, a transaction without a `COMMIT` marker is rolled back automatically: the WAL is truncated before the `BEGIN`, and the operations are discarded.

See [Store options](store-options.md#atomic-transactions) for the full reference.

## One database, one process

Opening a `Store` acquires an exclusive lock file (`{name}.lock.jigen`). A second `Store` on the same path — in the same or another process — fails to open with an `IOException`. This is a single-writer, embedded design: if you need multiple processes or machines to share a database, run the [server](../server/overview.md) instead and connect with `Jigen.Client`.

## Where to go next

- [Getting started](getting-started.md) — install the package and write a minimal example.
- [Store options](store-options.md) — full `StoreOptions` reference, durability, shrink, crash recovery, atomic transactions.
- [Collections](collections.md) — `VectorCollection<T>`, `DocumentCollection<T>`, filtering.
