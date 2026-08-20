# What is a vector database?

A **vector database** is a database designed to store, index, and search *embeddings* — high-dimensional vectors of floating-point numbers that represent the meaning of a piece of data (text, image, audio, ...). Instead of matching exact values like a relational database, it answers similarity questions: *"which stored items are closest to this query?"*

## The problem it solves

Traditional databases answer queries with **exact matches**: `WHERE name = 'Jigen'` either matches a row or it does not. That works for structured data, but not for unstructured content:

- how do you query "articles similar to this one" without knowing the exact words?
- how do you find images that show a cat when you only have a text description?
- how do you deduplicate near-identical product descriptions written differently?

The bridge from unstructured content to something a computer can compare is the **embedding** (see [embeddings overview](../embeddings/overview.md)): a neural model converts the input into a fixed-size vector (e.g. 768 floats) where **semantic similarity becomes geometric distance** — items that mean similar things end up close together in the vector space, regardless of the words or pixels they are made of.

A vector database is the system that stores those vectors and makes the "find the closest" operation fast and practical.

## How it works

1. **Embedding.** The content is converted to a vector by an embedding model. This can happen inside the database (Jigen's server can run ONNX text and image models in-process) or outside, in the application, with the vector handed to the database as-is.
2. **Storage.** Vectors and their associated content (the original document, metadata) are persisted. Jigen stores content and vectors in dedicated append-friendly files per collection, with crash recovery and checkpointing.
3. **Indexing.** An index organizes the vectors so that search does not have to scan everything. The default in Jigen is an **exact brute-force scan**; for larger collections you can plug in an **HNSW graph index** that answers approximately but much faster (see [ANN](ann.md) and [HNSW](hnsw.md)).
4. **Query.** A query vector is compared against the stored vectors with a **similarity metric** — Jigen uses **cosine similarity**, which measures the angle between vectors and ignores their magnitude. The database returns the `top-k` closest items with their scores.
5. **Filtering.** Queries can be combined with filters on document metadata (e.g. "similar articles, published in 2025, in Italian"), so similarity search and structured predicates work together.

## What makes it a database (not just a library)

A vector index alone is not a vector database. The database adds:

- **Persistence and durability** — vectors survive restarts; writes are checkpointed and crash-recovered (Jigen reconciles the index with the store after an unclean shutdown).
- **Concurrency** — safe parallel reads and writes, background indexing workers.
- **A query language** — similarity search, filters, multi-collection search, key-based access.
- **APIs** — Jigen runs **in-process** inside your .NET application (like SQLite for vectors) or as a **standalone server** with gRPC and REST endpoints and an official .NET client.

## Where Jigen fits

| Concern | How Jigen handles it |
|---|---|
| Embedding | ONNX text + image models in-process, or client-side vectors, or dedicated worker processes |
| Storage | Files per database/collection, memory-mapped, with checkpoints and crash recovery |
| Exact search | Default `BruteForceIndexer` — linear scan, perfect recall |
| Approximate search | `SmallWorldIndexer` (HNSW) — sublinear search time, small recall trade-off |
| Access | In-process `Store` API, or gRPC/REST server + `Jigen.Client` |
| Similarity | Cosine similarity throughout |

## See also

- [ANN](ann.md) — why approximate search exists and when to use it
- [HNSW](hnsw.md) — the graph algorithm Jigen uses for approximate search
- [In-process overview](../in-process/overview.md) — the Jigen engine architecture
- [Server overview](../server/overview.md) — deployment topologies
