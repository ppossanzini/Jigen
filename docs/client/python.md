# Python client

The Python package lives in `src/Client/Jigen.Python` and communicates with a
Jigen server over the same gRPC contract used by `Jigen.Client`.

It provides:

- synchronous `Context` and asynchronous `AsyncContext`;
- typed collection operations, bulk ingestion and streaming keys;
- vector search or server-embedded text search;
- equality, collection-membership, AND and OR filters;
- shared metadata records with grouped search;
- MessagePack compatibility with the .NET client;
- optional LangChain and LlamaIndex vector-store adapters.

## Sparse (lexical) vectors

The sparse slot is a second, independent vector on the same entry: caller-defined
term ids with their weights. The server never derives it from the text, and it
travels in the same write as the dense one.

```python
from jigen import SparseVector

sparse = SparseVector(terms=(12, 98, 450), weights=(0.8, 1.2, 0.4))

# Both vectors in one call.
collection.upsert("doc-1", {"title": "Jigen DB"}, embedding=dense_vector, sparse=sparse)

# Server-side embedding plus a lexical slot: the two are independent.
collection.upsert("doc-2", {"title": "Jigen DB"}, text="Jigen is a vector database.", sparse=sparse)

# Sparse only.
collection.upsert("doc-3", {"title": "Lexical"}, sparse=sparse)
```

Terms must be unique, strictly increasing and non-negative; weights finite,
non-negative, at least one above zero. `SparseVector.from_pairs` sorts the pairs
and rejects duplicates. The server validates again and enforces its configured
cap.

```python
# Exact lexical search.
hits = collection.search_sparse(sparse, top=10)

# Fusion of the dense and the lexical ranking.
fused = collection.search_hybrid(dense_vector, sparse, top=10, dense_weight=1.0, sparse_weight=1.0)
for hit in fused:
    print(hit.score, hit.in_dense, hit.in_sparse, hit.dense_score, hit.sparse_score)

# Read-back.
stored = collection.get_sparse("doc-1")   # None when the entry has no slot
```

A sentence fills whichever vector is missing, both generated server-side from the
same text:

```python
fused = collection.search_hybrid(sentence="mancano lampioni nel parco", top=10)
```

When both vectors are supplied explicitly, both weights must be given; a zero
weight excludes that modality. A sentence needs no weight. The async collection
exposes the same methods with `await`.

Writes of a sparse vector need the server's write-ahead log
(`JigenServer:Wal:Enabled`, on by default): without it the server refuses the
write, because the slot could not be recovered after a crash.

## Shared metadata

One record can back several embeddings. The record is database-scoped and
addressed by an opaque index; the association is per collection and key.

```python
passage = articles.create_metadata({"title": "Vector search", "body": "..."})

articles.associate_metadata("chunk-1", passage)
articles.associate_metadata("chunk-2", passage)

articles.update_metadata(passage, {"title": "Vector search", "body": "Revised once."})
```

`search_by_metadata` returns one result per record, ranked by the best score
among the associated embeddings. `candidate_budget` is keyword-only and
mandatory:

```python
groups = articles.search_by_metadata("vector database for .NET", top=10, candidate_budget=400)
for group in groups:
    print(group.metadata_index, group.score, group.content)
```

The async client exposes the same methods as coroutines. See the
[package README](https://github.com/ppossanzini/Jigen/blob/main/src/Client/Jigen.Python/README.md#shared-metadata)
for the full behaviour and the contract: fewer than `top` groups may be
returned when the budget is exhausted, and approximate indexes make no promise
of exact grouped top-k.

See the [package README](https://github.com/ppossanzini/Jigen/blob/main/src/Client/Jigen.Python/README.md) for complete
installation and usage examples.

