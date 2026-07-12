# HNSW index

`Jigen.Indexer.HNSW` (`SmallWorldIndexer`) is a disk-backed Hierarchical Navigable Small World (HNSW) approximate nearest-neighbor index, pluggable into `Store` in place of the default [brute-force index](brute-force.md). It trades a small amount of recall for search time that scales sub-linearly with collection size, at the cost of building and maintaining a graph.

HNSW organizes vectors into a multi-layer graph: sparse long-range links at the top layers let a search jump quickly across the space, converging to a dense, locally accurate layer at the bottom. Insertion and search both greedily descend the layers, refining the current best candidates as they go.

## Configuration

```csharp
using Jigen;
using Jigen.Indexer;

var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo",
  Indexer = new SmallWorldIndexer(new SmallWorldOptions
  {
    M = 16,
    EfConstruction = 200,
    EfSearch = 50,
    StoragePath = "/data/jigendb/hnsw"
  })
});
```

### SmallWorldOptions

| Parameter | Type | Default | Description |
|---|---|---|---|
| `M` | `int` | `10` (the bundled server sets `16`) | Maximum number of neighbors per node at layers above 0; the zero layer allows `2 × M`. Higher `M` improves recall at the cost of graph size and construction/search time. |
| `LevelLambda` | `double` | `1 / ln(M)` | Level-assignment decay parameter (`mL` in the HNSW paper). Derived from `M` by default. |
| `NeighbourHeuristic` | `NeighbourSelectionHeuristic` | `SelectHeuristic` | Neighbor-selection strategy at insertion. `SelectHeuristic` (Algorithm 4) gives the same recall as `SelectSimple` but ~2× faster search, at ~+30% construction cost. |
| `ConstructionPruning` / `EfConstruction` | `int` | `200` | Candidate list size considered when wiring a new node's neighbors (`efConstruction`). `EfConstruction` is an alias property for `ConstructionPruning`. |
| `SearchPruning` / `EfSearch` | `int` | `64` | Candidate list size at search time (`ef`). `EfSearch` is an alias property for `SearchPruning`. |
| `ExpandBestSelection` | `bool` | `false` | Whether to expand candidates during `SelectHeuristic` neighbor selection (`extendCandidates` in the paper). |
| `KeepPrunedConnections` | `bool` | `true` | Whether `SelectHeuristic` keeps pruned candidates to fill remaining neighbor slots (`keepPrunedConnections` in the paper). |
| `StoragePath` | `string` | `Path.Combine(Path.GetTempPath(), "jigen-hnsw")` | Directory for the per-collection graph files. |
| `InMemory` | `bool` | `false` | Keeps the graph entirely in memory instead of the disk-backed storage below (no persistence across restarts). |
| `Quantization` | `VectorQuantization` (`None` \| `SQ8`) | `None` | Scalar 8-bit quantization of the graph-side vectors. See [SQ8 quantization](#sq8-quantization). |
| `ExactRerank` | `bool` | `true` | With `SQ8`, rescores the final candidates using the store's full-precision embeddings before ranking. |
| `FilteredSearchExpansionFactor` | `int` | `20` | With a metadata filter, the filter is evaluated during level-0 traversal (ACORN-1 style): rejected nodes stay navigable but never enter the result window. This caps the expansion at `factor × ef` node visits, so a filter matching few or no documents degrades to a bounded partial scan instead of visiting the whole graph. Ignored without a filter. |
| `NodeCacheSize` | `int` | `65536` | Currently unused (kept for compatibility): graph nodes are resident in memory with vectors served from the memory-mapped `.vec` file, so there is no node cache to size. |
| `generator` | `Random` | `Random.Shared` | Random source used for level assignment. The default `Random.Shared` is sampled lock-free; setting a custom instance (e.g. seeded for deterministic graph construction in tests/benchmarks) re-enables a lock around it, since `Random` itself is not thread-safe. |

The server (`JigenServer:Index` configuration) applies its own defaults (`M: 16`, `EfConstruction: 200`, `EfSearch: 50`, ...) to every database it opens — see [Server configuration](../server/configuration.md).

## Disk layout

Each collection's graph is split across two files under `StoragePath`, named after the (sanitized) collection name:

| File | Contents |
|---|---|
| `{collection}.hnsw.vec` | Immutable vector part: id, level, and the vector (or quantized vector) payload. **Append-only** — a node's vector record is written once at insertion and never rewritten. |
| `{collection}.hnsw.adj` | Mutable adjacency part: deletion flag and per-level neighbor lists, plus the graph entrypoint pointer (meaningful for slot 0 only). Each level's connection list is serialized at its **fixed capacity** and padded, so every update is an in-place overwrite — no relocation, no dead space, no file growth from re-wiring. |

Vectors are read back through a memory-mapped view of `.vec` (`MappedVectorFile`) rather than deserialized into managed arrays — nodes read their vector zero-copy once a remap covers their offset (freshly inserted nodes keep a small in-memory staging copy until then).

Position 0 in the node list is reserved: it stores the graph's **entrypoint pointer** rather than a real node, so the entrypoint can be swapped (e.g. after all high-level nodes are deleted) without relocating any other node's index.

An older single-file graph format (`{collection}.hnsw`) is transparently migrated to the split `.vec`/`.adj` layout the first time the collection is opened, then removed.

## Concurrent inserts

Insertion follows an hnswlib-style locking scheme: a graph-wide lock covers only node allocation and entrypoint reassignment; per-node locks guard adjacency wiring. Two inserts touching different nodes proceed fully in parallel, and — because locking is per node rather than per collection — **a single collection scales across every configured `StoreOptions.IndexerWorkers`**, not just across collections. The lock order is always graph → node → storage, to avoid deadlocks.

## Delete support

Deletes are logical: `RemoveFromIndex` flags the node's `IsDeleted` bit (persisted via an adjacency-record rewrite) rather than removing it from the graph — removing a node from an HNSW graph outright would require re-wiring all of its neighbors. Deleted nodes:

- are skipped when the graph is traversed for descent and results,
- remain as internal navigation waypoints as long as needed,
- trigger promotion of a new entrypoint if the deleted node was the current one.

A lazily-built key → position lookup (built on the first delete for a collection) makes delete-by-key O(1) instead of a full graph scan.

## SQ8 quantization

With `Quantization = VectorQuantization.SQ8`, newly written graph vectors are unit-normalized and quantized to `sbyte` (scale 127, components in [-1, 1]) instead of stored as `float`. This is a graph-only trade-off — **store embeddings are never touched**, so exact vectors remain available for reranking or for a brute-force fallback.

Trade-offs:

- **~4× smaller** graph vector file (`sbyte` vs `float`), and cheaper SIMD int8 dot products during graph traversal.
- Small recall cost from the reduced precision, recovered by `ExactRerank` (default `true`): the final candidate set is rescored with the store's full-precision embeddings (via `Store.GetEmbedding`) before the results are ranked and returned.
- A graph can hold a mix of `float` and `SQ8` records if quantization is turned on mid-life; a scalar mixed-precision dot product path handles that case (slower, compatibility-only).

```csharp
var options = new SmallWorldOptions
{
  M = 16,
  Quantization = VectorQuantization.SQ8,
  ExactRerank = true // default; keep enabled unless you accept the recall drop
};
```

## Tuning guidance

- **`M`** — the main recall/memory/build-time knob. Higher `M` (e.g. 16–32) gives a denser graph with better recall, at the cost of more memory (adjacency lists) and slower construction and search per node visited. The bundled server defaults to `16`, higher than the library's own default of `10`.
- **`EfConstruction`** — larger values build a higher-quality graph (more candidates considered per insertion) at a roughly linear construction-time cost. `200` is a reasonable default for most collections.
- **`EfSearch`** — the main recall/latency knob at query time, and the cheapest one to tune since it has no effect on the stored graph. Raise it for larger collections or when recall matters more than latency; lower it to trade recall for speed. `Store.Search` internally uses `max(top, EfSearch)`, so `EfSearch` also acts as a floor on how many candidates are considered regardless of how few results are requested.
- Combine `M`/`EfConstruction` (build-time) with `EfSearch` (query-time) to hit a target recall: increase `EfSearch` first (free to change per query, no rebuild needed) before increasing `M`/`EfConstruction` (requires rebuilding/growing the graph).
- Use SQ8 quantization when the graph's memory footprint or scan cost dominates and a small, `ExactRerank`-recovered recall trade-off is acceptable.

## See also

- [Brute-force index](brute-force.md) — the exact alternative, useful as a recall baseline.
- [Store options](../in-process/store-options.md) — `IndexerWorkers`, crash recovery/reconciliation (the HNSW indexer participates in `ReconcileIndexAsync`).
- [Collections](../in-process/collections.md) — filtering limits shared across indexers.
