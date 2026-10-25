# Python client

The Python package lives in `src/Client/Jigen.Python` and communicates with a
Jigen server over the same gRPC contract used by `Jigen.Client`.

It provides:

- synchronous `Context` and asynchronous `AsyncContext`;
- typed collection operations, bulk ingestion and streaming keys;
- dense, sparse and hybrid search, plus server-embedded text search;
- sparse `(term id, weight)` pairs on write, for the collection's sparse index;
- equality, collection-membership, AND and OR filters;
- MessagePack compatibility with the .NET client;
- optional LangChain and LlamaIndex vector-store adapters.

## Search modes

```python
hits = articles.search_dense([0.14, -0.29, 0.84], top=5)          # dense half only
hits = articles.search_dense("vector database for .NET", top=5)   # dense, server embeds the text
hits = articles.search_sparse({1204: 1.0, 77: 0.4}, top=5)        # sparse half only
hits = articles.search([0.14, -0.29, 0.84], {1204: 1.0})          # hybrid, explicit halves
hits = articles.search("vector database", {1204: 1.0})            # hybrid, server embeds the text
```

The three names match the in-process engine: `search_dense`, `search_sparse` and
`search` for the hybrid; `search_hybrid` remains as an alias of `search`. With
`sparse_from_sentence=True` the server derives the sparse half from the same
sentence it embeds densely, so a hybrid search needs nothing but text:

```python
hits = articles.search("how do I reset my password?", top=10, sparse_from_sentence=True)
```

A hybrid hit exposes the fused `score` together with the partial `dense_score`
and `sparse_score`, and `SearchOptions(min_dense_score=..., min_sparse_score=...)`
drops the weak hits of each half before the fusion. `AsyncVectorCollection`
exposes the same methods with `await`.

## Sparse terms on write

```python
articles.upsert("a", {"title": "A"}, embedding=[0.1, 0.2], sparse={1204: 1.0, 77: 0.4})
articles.upsert("b", {"title": "B"}, embedding=[[0.1], [0.2]], sparse=[{1204: 1.0}, {77: 0.4}])
articles.upsert("c", {"title": "C"}, text="sparse retrieval", sparse={1204: 1.0})
articles.upsert_many([("d", {"title": "D"}, [0.3, 0.4], {1204: 1.0})])

# Let the server derive the sparse terms from the sentence it embeds.
articles.upsert("e", {"title": "E"}, text="a sentence", sparse_from_sentence=True)
```

A one-vector entry takes one mapping, a multi-vector entry one mapping per
vector; a mapping longer than the vector list or a negative term id raises
`ValueError`. Term ids are your vocabulary: a query only matches documents
indexed with the same ids.

See the [package README](https://github.com/ppossanzini/Jigen/blob/main/src/Client/Jigen.Python/README.md) for complete
installation and usage examples.

