# Brute-force index

`BruteForceIndexer` is the default index used by `Store` when no `Indexer` is configured in `StoreOptions`. It performs an exact, exhaustive search over a collection's embeddings — no graph to build, no approximation, no tuning.

## How it works

`BruteForceIndexer` does no work on write: `AddToIndex` and `RemoveFromIndex` are no-ops, since every search reads directly from the store's embeddings file. All the cost is paid at search time.

Search ranks candidates by **cosine similarity** (`System.Numerics.Tensors.TensorPrimitives.CosineSimilarity`), the same metric the HNSW indexer uses, computed directly against the memory-mapped embeddings file — no vectors are copied into managed arrays before scoring.

Two code paths, chosen automatically:

- **Unfiltered search** — a parallel, bounded top-k selection: each worker thread keeps a fixed-size min-heap of the `top` best (offset, score) pairs it has seen; heaps are merged at the end, and only the winning entries' ids are read back from disk. This avoids materializing all ids and avoids an O(N log N) full sort.
- **Filtered search** (a content filter is supplied) — every candidate is scored and ranked first (`RankAll`), because the top-k cannot be decided from scores alone: entries that fail the filter must be skipped and backfilled from further down the ranking. Results stream out, filter-checked and counted, until `top` matches are found or candidates run out.

Both paths run the scoring loop with `Parallel.ForEach` across the collection's position index.

## When to prefer it

- **Small to medium collections**, where a full scan is cheap enough (its cost scales with the number of embeddings held in the collection, not with any tunable parameter).
- **When exactness matters** more than raw search latency: brute force never trades recall for speed, unlike an approximate index.
- **As a correctness baseline** when validating an HNSW-backed collection (e.g. computing recall in benchmarks, as done in `tests/JigenBenchmarks`).

For larger collections where scan cost becomes the bottleneck, switch to the [HNSW index](hnsw.md), which trades a small amount of recall for sublinear search time.

## Usage

Brute force is the default — no explicit configuration is required:

```csharp
using Jigen;

var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo"
  // Indexer left unset: defaults to new BruteForceIndexer()
});
```

It can also be set explicitly, or swapped back at any time (the indexer is a stateless reader of the store's embeddings, so switching between brute force and HNSW just changes the `StoreOptions.Indexer` used for the next `Store` instance):

```csharp
var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo",
  Indexer = new BruteForceIndexer()
});
```

## Filtered search caveat

Because a content filter forces full ranking (`RankAll`) instead of the bounded top-k selection, filtered searches over large collections cost more than unfiltered ones — proportional to the whole collection, not to `top`. See [Collections](../in-process/collections.md#limits) for the filtering expression limits shared with the HNSW indexer.

## See also

- [HNSW index](hnsw.md) — approximate nearest-neighbor alternative for larger collections.
- [Collections](../in-process/collections.md) — `VectorCollection<T>`/`DocumentCollection<T>` and filtering.
