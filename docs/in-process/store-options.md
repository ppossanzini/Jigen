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
| `Wal` | `WalOptions` | `null` (disabled) | Write-Ahead Log configuration — see [Write-Ahead Log](#write-ahead-log-wal) below. |

## Files on disk

A `Store` named `{name}` under `DataBasePath` creates:

| File | Contents |
|---|---|
| `{name}.content.jigen` | Document payloads (append-only; a header records the current write position). |
| `{name}.vectors.jigen` | Embeddings (append-only; layout `[id length][id][dimensions × float]`). |
| `{name}.index.jigen` | Position index log: one record per write/delete, mapping a key to its content and embedding offsets (or a tombstone). Replayed in full at startup to rebuild the in-memory position index. |
| `{name}.lock.jigen` | Exclusive lock for the database's lifetime; also the crash marker (see below). |
| `{name}.wal.jigen` | **Optional** Write-Ahead Log: CRC32-protected, append-only, written *before* the ingestion queue for per-record durability. Truncated after each checkpoint. Only created when `WalOptions.Enabled = true`. |

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

## Write-Ahead Log (WAL)

The WAL is an **independent durability layer** that sits between the public API and the ingestion queue. When enabled, every write is persisted to the WAL file **before** being enqueued for background writing to the data files.

```
AppendContent(entry):
  1. Serialize + write to {name}.wal.jigen    ← CRC32-protected, configurable fsync
  2. Enqueue to IngestionQueue                ← async flush to data files
  3. WriterThread writes to data files         ← content/vectors/index (unchanged)
```

The WriterThread is **completely unaware** of the WAL — it only sees the IngestionQueue. This separation means the WAL can be added or removed without touching the writer logic.

### WalOptions

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `false` | Enable the WAL. When false, no `.wal.jigen` is created and AppendContent returns after enqueuing to the ingestion queue (no per-record durability). |
| `Durability` | `WalDurability` | `Group` | `None` — no fsync on WAL writes (durability only at checkpoint). `Group` — fsync every `MaxGroupDelay` or `MaxGroupBatchCount` writes. `PerWrite` — fsync after every write. |
| `MaxGroupDelay` | `TimeSpan` | `10ms` | Maximum time between fsync calls in Group mode. |
| `MaxGroupBatchCount` | `int` | `8` | Maximum write count before a forced fsync in Group mode. |
| `CheckpointInterval` | `TimeSpan` | `30s` | Interval between automatic checkpoints. |

```csharp
var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "mydb",
  Wal = new WalOptions
  {
    Enabled = true,
    Durability = WalDurability.Group
  }
});
```

### Checkpoint

A background thread (`WalCheckpointer`) runs every `CheckpointInterval`:

1. Waits for the ingestion queue to drain (`WaitForWritingCompleted` + `WaitForIndexingCompleted`)
2. Takes the writer's I/O lock
3. `fsync`s content, vectors, and index files to disk
4. Writes a checkpoint marker (`0xFE`) to the WAL
5. Truncates the WAL (all data before this point is now durable in the data files)

After a clean shutdown (`Close()` calls `ForceCheckpoint()`), the WAL is empty.

### Recovery

On startup after a crash:

1. `LoadIndex()` — rebuilds the in-memory PositionIndex from `index.jigen` (consolidated state up to the last checkpoint)
2. `ReplayWal()` — feeds WAL records after the last checkpoint into the IngestionQueue. The WriterThread (started after replay) processes them normally, writing to content/vectors/index. Deletes and ClearCollections are applied as in-memory tombstone operations.

```
Recovery flow:
  LoadIndex()        → PositionIndex = snapshot at last checkpoint
  ReplayWal():
    Insert records   → IngestionQueue.Enqueue()  ← WriterThread will write
    Delete records   → PositionIndex.Remove() + index tombstone
    ClearCollection  → PositionIndex.Remove() + index tombstones
```

The WAL **never writes to content.jigen or vectors.jigen directly** — only the WriterThread touches those files, preserving a single source of truth.

### Atomic transactions

When the WAL is enabled, `Store.BeginTransaction()` returns a `Transaction` that buffers all operations in memory until committed. At `CommitAsync()`, the entire transaction is serialized to the WAL as a single atomic block:

```
WAL layout for a transaction:
  [BEGIN txId][Insert record][Insert record][Delete record][COMMIT txId]
```

All records inside the same `Write()` syscall — if the process crashes mid-write, the torn tail is detected by CRC mismatch and the entire transaction is discarded on recovery.

```csharp
using var tx = store.BeginTransaction();
tx.Append(new VectorEntry { Id = key1, CollectionName = "docs", … });
tx.Append(new VectorEntry { Id = key2, CollectionName = "docs", … });
tx.Delete("docs", key3);
await tx.CommitAsync();  // all-or-nothing: BEGIN→ops→COMMIT in one Write()
```

**Rollback**: If `Rollback()` is called (or `Dispose` without `CommitAsync`), the buffered operations are discarded without touching the WAL.

**Recovery**: During `ReplayWal()`, when a `BEGIN` marker is encountered, subsequent records are buffered in memory. Only when the matching `COMMIT` is found are they applied atomically (enqueued into `IngestionQueue` for inserts, applied as tombstones for deletes). If the WAL ends without a `COMMIT`, the incomplete transaction is rolled back: the WAL is truncated before the `BEGIN`, and the buffered records are discarded.

**Constraints**:
- Nested transactions are not supported — a `BEGIN` inside an open transaction stops replay at the previous valid position.
- `ClearCollection` inside a transaction is not supported — treated the same as nested transactions.
- Transactions require the WAL to be enabled — `CommitAsync()` throws `InvalidOperationException` if `WalOptions.Enabled` is `false`.

### Performance

Benchmarked on 10k vectors (128d), HNSW M=16, NVMe, .NET 10:

| Mode | Ingest | Search | Delete | Disk overhead |
|---|---|---|---|---|
| WAL off | 1,697 vec/s | 724 µs/q | 41 µs/del | — |
| WAL None | 1,574 vec/s | 881 µs/q | 7,722 µs/del | +5.4 MB |
| WAL Group | 1,560 vec/s | 822 µs/q | 6,672 µs/del | +5.4 MB |
| WAL PerWrite | 613 vec/s | 853 µs/q | 6,596 µs/del | +5.4 MB |

Ingest throughput with WAL Group is ~8% lower than without WAL (1,560 vs 1,697 vec/s) — the cost of one additional `Write` syscall per record to the WAL file. Delete latency increases because each delete writes a separate WAL record.

The disk overhead is the WAL file itself: each Insert record carries the full content and embedding payload (duplicated from the data files). This is the trade-off of write-ahead logging: the WAL is a complete copy of every uncheckpointed write.

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
