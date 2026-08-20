# What is ANN (Approximate Nearest Neighbor)?

**Nearest-neighbor search** finds the stored vectors closest to a query vector. **Approximate Nearest Neighbor (ANN)** search finds vectors that are *almost* the closest ones — close enough for practical purposes — in a fraction of the time an exact search would need.

## Exact search and its cost

The exact answer is simple: compare the query against **every** stored vector and keep the best `k`. This is what Jigen's default [brute-force index](../indexes/brute-force.md) does, and it is perfectly accurate.

The problem is scaling. With `N` vectors of dimension `D`, an exact search costs `O(N × D)` operations:

| Vectors | ~Time (CPU, 768-dim, brute force) |
|---|---|
| 10,000 | ~1 ms |
| 1,000,000 | ~100 ms |
| 100,000,000 | ~10 s |

Beyond a few million vectors, an exact scan stops being practical for interactive queries. This is where ANN comes in.

## The core trade-off: recall vs. latency

ANN indexes trade a small, bounded amount of accuracy for much faster search. Two numbers define the trade-off:

- **Recall** — the fraction of the *true* top-k that the approximate search returns. 95–99% recall is typical and usually indistinguishable in practice from exact results.
- **Latency / QPS** — how fast queries are, and how many can be served per second.

The same index often exposes a **quality knob** that moves along this curve: raise it for higher recall (slower), lower it for higher throughput. In HNSW that knob is `EfSearch` — see [HNSW](hnsw.md).

## Why not just brute force forever?

- **Latency**: interactive features (search-as-you-type, chat retrieval) need single-digit-millisecond answers.
- **Throughput**: serving thousands of queries per second on a shared machine.
- **Memory/bandwidth**: scanning the whole vector file per query saturates memory bandwidth long before CPU time does.

Exact search remains the right choice for small collections, for correctness baselines, and whenever the collection is small enough that scanning is cheap. That is why Jigen keeps brute force as its default.

## Families of ANN algorithms

| Family | Idea | Examples |
|---|---|---|
| **Graph-based** | Build a graph where similar vectors are linked; walk it greedily from an entry point. | **HNSW** (used by Jigen), NSW |
| **Partition / clustering** | Split the space into cells (e.g. k-means); probe only the cells near the query. | IVF (inverted file index) |
| **Hashing** | Map similar vectors to the same buckets with locality-sensitive hashing (LSH). | LSH |
| **Quantization** | Compress vectors (e.g. 8-bit scalars, product quantization) so more fit in memory and distance math is cheaper. | SQ8 (used by Jigen's HNSW), PQ |

These techniques are often **combined**: Jigen's HNSW graph, for example, can store its vectors SQ8-quantized to shrink memory bandwidth, with an optional exact rerank step to recover precision.

## How Jigen implements ANN

- **Default**: exact [brute-force index](../indexes/brute-force.md) — no approximation at all.
- **Optional**: [HNSW index](../indexes/hnsw.md) (`SmallWorldIndexer`) — a graph-based ANN with `M`/`EfConstruction`/`EfSearch` knobs, SQ8 quantization with exact rerank, and disk-backed storage.
- **Server-side**: the HNSW parameters are set once per server in `JigenServer:Index` and applied to every database; a `LazyHnswThreshold` option defers graph construction entirely during bulk loads.

## See also

- [HNSW](hnsw.md) — the specific ANN algorithm used by Jigen, explained
- [Vector databases](vector-database.md) — where ANN fits in a vector database
- [HNSW index](../indexes/hnsw.md) — Jigen's implementation and parameter reference
