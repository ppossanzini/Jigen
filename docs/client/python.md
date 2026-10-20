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

