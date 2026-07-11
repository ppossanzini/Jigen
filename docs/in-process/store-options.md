# Store options

`StoreOptions` configures a `Store`: where its files live, which index it uses, and how it handles durability, compaction, and crash recovery.

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `DataBasePath` | `string` | — | Directory holding the database files. |
| `DataBaseName` | `string` | — | Base name for the database files (`{name}.content.jigen`, etc.). |
| `Indexer` | `IIndexer` | `new BruteForceIndexer()` | The search index backing the store. Swap for `SmallWorldIndexer` (`Jigen.Indexer.HNSW`) for approximate nearest-neighbor search — see [HNSW index](../indexes/hnsw.md). |
| `AutoShrink` | `bool` | `false` | When true, `SaveChangesAsync` triggers `ShrinkAsync` automatically once both shrink thresholds below are exceeded. |
| `ShrinkMinDeadBytes` | `long` | `64 * 1024 * 1024` (64 MiB) | Minimum dead bytes (from deletes and overwrites) before a shrink is considered worthwhile. |
| `ShrinkFragmentationThreshold` | `double` | `0.4` | Minimum dead/total byte ratio of the data files before a shrink is considered worthwhile. |
| `IndexerWorkers` | `int` | `clamp(ProcessorCount / 2, 1, 8)` | Number of background threads that build the index off the writer thread. Entries are distributed round-robin; the HNSW indexer supports concurrent inserts, so a single collection scales across all workers. |
| `ReconcileOnUncleanShutdown` | `bool` | `true` | When true, opening a database that was not closed cleanly automatically reconciles the index with the store content before the constructor returns. |

## Files on disk

A `Store` named `{name}` under `DataBasePath` creates:

| File | Contents |
|---|---|
| `{name}.content.jigen` | Document payloads (append-only; a header records the current write position). |
| `{name}.vectors.jigen` | Embeddings (append-only; layout `[id length][id][dimensions × float]`). |
| `{name}.index.jigen` | Position index log: one record per write/delete, mapping a key to its content and embedding offsets (or a tombstone). Replayed in full at startup to rebuild the in-memory position index. |
| `{name}.lock.jigen` | Exclusive lock for the database's lifetime; also the crash marker (see below). |

If an HNSW indexer is configured, it maintains its own files per collection under its `StoragePath` — see [HNSW index](../indexes/hnsw.md).

## Durability model

Writes do not hit disk synchronously. `AppendContent` places the entry on an in-memory ingestion queue and returns; a single writer thread drains the queue in batches (content + embedding + index appends), and hands each entry off to a pool of `IndexerWorkers` background threads that build the index entry. A background flusher also flushes the file streams to the OS every 30 seconds while the writer is idle.

This gives three distinct guarantees, from weakest to strongest:

1. **Accepted** — `AppendContent` returned; the entry is on the queue.
2. **Persisted and indexed** — the writer thread and the index workers have processed it; it is visible to reads and searches, but not yet fsynced.
3. **Durable** — `SaveChangesAsync()` has completed: it waits for the ingestion queue and the indexing pipeline to drain, flushes the index (`Indexer.FlushAsync()`), and fsyncs the content, embeddings, and index files.

```csharp
await store.SaveChangesAsync();
```

Call it whenever you need a checkpoint (e.g. periodically, or before a controlled shutdown); `Close()` performs an equivalent flush automatically.

### Ingestion errors

The writer and index workers run on background threads and never let an exception escape (a stalled writer would block producers on a full queue). Instead, the last failure is recorded and surfaced the next time `SaveChangesAsync` is called:

```csharp
public Exception IngestionError => Writer.LastError;
```

`SaveChangesAsync` throws an `IOException` (wrapping the recorded failure) if any queued entry failed to persist or index since the last checkpoint — check `IngestionError` proactively if you need to inspect it without triggering the throw.

### Deletes

`DeleteContent` (and `VectorCollection<T>.Remove` / `DocumentCollection<T>.Remove`) run inline rather than through the queue: they wait for the writer and indexer pipelines to drain first (so an in-flight append of the same key cannot resurrect it), then write a tombstone record to the index log and remove the key from the in-memory index. The tombstone becomes durable at the next `SaveChangesAsync`/`Close`, like appended entries (group commit — no per-delete fsync).

## Shrink

Deletes and overwrites leave old content/embedding records unreachable ("dead bytes") until reclaimed. `Store` exposes:

- `DeadBytes` — bytes made unreachable by deletes and overwrites.
- `FragmentationRatio` — dead/total ratio of the content and embeddings files.
- `NeedsShrink` — true once both `ShrinkMinDeadBytes` and `ShrinkFragmentationThreshold` are exceeded.
- `ShrinkAsync()` — compacts the content, embeddings, and index files by copying live records to fresh files and swapping them in with atomic renames. Crash-safe: the originals stay intact until the rename. Ingestion is paused for the duration.

With `AutoShrink = true`, `SaveChangesAsync` calls `ShrinkAsync()` automatically whenever `NeedsShrink` is true. With the default `AutoShrink = false`, call `ShrinkAsync()` yourself when appropriate.

```csharp
if (store.NeedsShrink)
  await store.ShrinkAsync();
```

## Crash recovery and reconciliation

The lock file (`{name}.lock.jigen`) is deleted on a clean `Close()`. If a process crashes or is killed, the lock file survives — its presence at the next `Store` construction is the signal that the previous run may have left the on-disk state inconsistent (e.g. an index update that never made it to the graph).

```csharp
public bool WasUncleanShutdown => _uncleanShutdown;
```

When `ReconcileOnUncleanShutdown` is `true` (the default) and the previous shutdown was unclean, the constructor runs `ReconcileIndexAsync()` before returning: it re-indexes store entries whose index update was lost, and drops index entries whose key no longer exists in the store. This runs synchronously as part of opening the store, so opening after a crash can take noticeably longer on large collections.

```csharp
var store = new Store(options);
if (store.WasUncleanShutdown)
{
  // Reconciliation already ran (if ReconcileOnUncleanShutdown was true).
}
```

`ReconcileIndexAsync()` can also be called manually at any time (e.g. with `ReconcileOnUncleanShutdown = false`, to run it under your own control or logging).

## See also

- [In-process overview](overview.md) — architecture and storage model.
- [Collections](collections.md) — typed access on top of a `Store`.
- [Brute-force index](../indexes/brute-force.md) and [HNSW index](../indexes/hnsw.md) — the pluggable `Indexer`.
