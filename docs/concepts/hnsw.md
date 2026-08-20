# What is HNSW?

**Hierarchical Navigable Small World (HNSW)** is a graph-based **Approximate Nearest Neighbor** algorithm (see [ANN](ann.md)). It organizes vectors into a multi-layer graph where searches "zoom in" from coarse to fine, and it is the algorithm behind Jigen's `SmallWorldIndexer`.

HNSW is the algorithm most vector databases use (or a derivative of it) because it offers an excellent balance of search speed, recall, build cost, and memory.

## The intuition

Imagine navigating a foreign city with three maps:

1. a **world map** — you can jump between continents in one step;
2. a **country map** — you move between cities;
3. a **street map** — you walk door to door.

You never search the street map exhaustively: you land on the right continent, descend to the right country, and only then walk the streets. HNSW does exactly this in vector space.

The algorithm builds **layers** of the graph:

- the **top layer** has few nodes and long links — fast jumps across the whole space;
- each layer below has more nodes and shorter links, down to **layer 0**, which contains every vector and connects each to its nearest neighbors.

## How a search works

A query arrives as a vector:

1. Start at the **entry point** (a node on the top layer).
2. **Greedy descent**: at each node, move to the neighbor that is closest to the query; repeat until no neighbor improves the distance.
3. Drop to the next layer and repeat, refining the position.
4. At **layer 0**, keep a candidate list of the best nodes seen, expanding greedily, until the search budget is exhausted.

The quality knob is **`ef` (search beam width, `EfSearch` in Jigen)**: how many candidates are tracked at layer 0. More candidates → better recall, slower search. This is a *query-time* parameter: changing it needs no rebuild.

## How insertion works

A new vector is inserted the same way a search is:

1. Walk down the layers greedily (the same search procedure).
2. Assign the node a **random level** with an exponentially decaying distribution (`mL` in the paper, `LevelLambda` in Jigen) — most nodes live only at layer 0, few reach the top.
3. At each layer the node belongs to, connect it to the `M` closest candidates found during the descent, using a neighbor-selection heuristic that keeps connections diverse (not all pointing in one direction).

**`M`** is the connection budget per node: higher `M` gives a denser graph with better recall, at the cost of memory and build time. **`efConstruction`** is the search beam used *during* insertion — a larger value builds a higher-quality graph, at the cost of slower ingestion.

## Why it works so well

- **Logarithmic-ish search**: the top layers skip huge regions of the space in one hop, so search cost grows sub-linearly with the number of vectors.
- **Small-world property**: any node can reach any other in few hops — long-range links at the top make the graph navigable.
- **No global structure to maintain**: unlike tree or clustering methods, there is no root split or centroid to rebuild; vectors are added incrementally, which makes it friendly to streaming ingestion and deletes (Jigen supports logical deletes, see the [HNSW index](../indexes/hnsw.md) page).

## The three knobs (recap)

| Parameter | Phase | Effect |
|---|---|---|
| `M` | Build | Connections per node. Higher → better recall, more memory, slower build and search per hop. |
| `EfConstruction` | Build | Beam width during insertion. Higher → higher-quality graph, slower ingestion. |
| `EfSearch` | Query | Beam width during search. Higher → better recall, slower search. Free to change per query, no rebuild. |

Rule of thumb: **tune `EfSearch` first** (it is per-query and cheap), then raise `M`/`EfConstruction` only if recall is still insufficient and a rebuild/growth cost is acceptable.

## See also

- [ANN](ann.md) — the broader family of approximate search algorithms
- [HNSW index](../indexes/hnsw.md) — Jigen's implementation: disk layout, SQ8 quantization, concurrency, deletes, full parameter reference
- [Server configuration](../server/configuration.md) — how the server applies HNSW settings (`JigenServer:Index`)
