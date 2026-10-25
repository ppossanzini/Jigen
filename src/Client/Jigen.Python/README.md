# Jigen Python client

`jigen-client` is the Python gRPC client for Jigen DB. Its core API mirrors the
.NET client while remaining idiomatic in Python, and optional adapters expose
Jigen as a vector store to LangChain and LlamaIndex.

## Install

```bash
pip install jigen-client

# Optional integrations
pip install "jigen-client[langchain]"
pip install "jigen-client[llama-index]"
```

Python 3.10 or newer is required.

## Connect

```python
from jigen import ConnectionOptions, Context

context = Context(ConnectionOptions(
    host="localhost",
    port=3223,
    database="demo",
))
articles = context.collection("articles")
```

Use `AsyncContext` in asyncio applications. Both contexts are context managers
and close channels they create.

## Insert and search

Use an existing embedding:

```python
articles.upsert(
    "article-42",
    {"title": "Vector search", "tags": ["rag", "dotnet"]},
    embedding=[0.12, -0.31, 0.88],
)

hits = articles.search([0.14, -0.29, 0.84], top=5)
```

Or let Jigen's server-side ONNX module embed the text:

```python
articles.upsert(
    "article-42",
    {"title": "Vector search", "tags": ["rag", "dotnet"]},
    text="A practical introduction to vector search in .NET",
)

hits = articles.search("vector database for .NET", top=5)
```

Batch operations use one client-streaming RPC:

```python
accepted = articles.upsert_many([
    ("a", {"title": "A"}, embedding_a),
    ("b", {"title": "B"}, embedding_b),
])
```

### Single and multi-vector entries

The same `embedding` argument accepts one vector or a list of vectors; every
vector of an entry must share the collection dimensions.

```python
# One vector.
articles.upsert("single", {"title": "Single vector"}, embedding=[1.0, 0.0, 0.0])

# Several vectors, e.g. one per passage.
articles.upsert("multi", {"title": "Multi vector"}, embedding=[
    [1.0, 0.0, 0.0],   # passage 1
    [0.0, 1.0, 0.0],   # passage 2
    [0.0, 0.0, 1.0],   # passage 3
])

articles.get_embeddings("multi")  # [[1,0,0], [0,1,0], [0,0,1]]

articles.search([0.0, 1.0, 0.0], top=2)  # finds "multi" through its second vector
```

`upsert_many` accepts both shapes per entry, so a batch can mix them:

```python
articles.upsert_many([
    ("a", {"title": "A"}, [0.5, 0.5, 0.0]),
    ("b", {"title": "B"}, [[0.5, 0.5, 0.0], [0.0, 0.5, 0.5]]),
])
```

An entry is always a list of vectors: one stored vector comes back as a list of
one, and there is no single-vector reader. Search scores a document by its
closest vector.

### Dense, sparse and hybrid search

The three names match the in-process engine: `search_dense`, `search_sparse` and
`search` for the hybrid search (`search_hybrid` stays as an alias of `search`).

```python
hits = articles.search_dense([0.14, -0.29, 0.84], top=5)  # dense, by vector
hits = articles.search_dense("vector database for .NET", top=5)  # dense, by text
```

`search_sparse` runs the lexical half only. The query maps term identifiers to
weights; those identifiers are the ones Jigen's server-side sparse index assigns
to terms, so the client receives them from the same tokenizer that built it:

```python
hits = articles.search_sparse({1204: 1.0, 77: 0.4}, top=5)
```

`search` fuses both halves with reciprocal rank; either half may be omitted, but
not both. With `sparse_from_sentence=True` the server derives the sparse half
from the sentence it embeds, so a hybrid search needs nothing but text:

```python
hits = articles.search([0.14, -0.29, 0.84], {1204: 1.0}, top=5)
hits = articles.search("vector database", {1204: 1.0}, top=5)  # server embeds the text
hits = articles.search("how do I reset my password?", top=5, sparse_from_sentence=True)
```

Each hit carries the fused `score` plus the partial `dense_score` and
`sparse_score` that produced it, when the server computed them. Use
`min_dense_score` and `min_sparse_score` to drop weak candidates before the
fusion, and `min_score` for a floor on the fused score:

```python
from jigen import SearchOptions

hits = articles.search(
    query_vector,
    {1204: 1.0},
    top=10,
    options=SearchOptions(min_dense_score=0.3, min_sparse_score=0.05),
)
```

Async collections expose the same three methods with `await`.

### Writing sparse terms

The sparse pairs a document carries are supplied by the writer: pass `sparse`
on `upsert` and `append` as a mapping of term id to weight. A one-vector entry
takes one mapping; a multi-vector entry takes a list with one mapping per
vector.

```python
articles.upsert(
    "article-42",
    {"title": "Vector search"},
    embedding=[0.12, -0.31, 0.88],
    sparse={1204: 1.0, 77: 0.4},
)

# One sparse list per vector, aligned by index.
articles.upsert(
    "article-43",
    {"title": "Multi vector"},
    embedding=[[0.12, -0.31, 0.88], [0.4, 0.1, -0.2]],
    sparse=[{1204: 1.0}, {77: 0.4}],
)

# A document whose embedding the server computes.
articles.upsert("article-44", {"title": "By text"}, text="sparse retrieval", sparse={1204: 1.0})

# Both slots from the sentence: the server embeds it densely and derives the sparse terms.
articles.upsert("article-45", {"title": "Derived"}, text="sparse retrieval", sparse_from_sentence=True)

# Bulk: the fourth item is optional.
articles.upsert_many([
    ("a", {"title": "A"}, [0.1, 0.2], {1204: 1.0}),
    ("b", {"title": "B"}, [0.3, 0.4]),
])
```

Term ids must be non-negative and must be the ids the queries use: a mapping
longer than the vector list, or a single mapping on a multi-vector entry, is
refused instead of being attached to a vector that does not exist.

## Filters and tuning

```python
from jigen import SearchOptions, contains, equals

where = equals("category", "guide") & contains("tags", "rag")
hits = articles.search(
    query_vector,
    top=10,
    filter=where,
    options=SearchOptions(ef_search=200, min_score=0.35),
)
```

Filter paths refer to fields in the serialized document. Supported operations
match Jigen's protocol: equality, collection membership, AND and OR.

## LangChain

```python
from jigen.integrations.langchain import JigenVectorStore

# Uses server-side embeddings when no LangChain Embeddings object is supplied.
store = JigenVectorStore(context.collection("knowledge"))
store.add_texts(
    ["Jigen stores HNSW graphs on disk."],
    metadatas=[{"source": "hnsw.md"}],
    ids=["hnsw-1"],
)
retriever = store.as_retriever(search_kwargs={"k": 4})
```

Pass `embedding=OpenAIEmbeddings(...)` (or any LangChain `Embeddings`) to keep
embedding generation client-side.

## LlamaIndex

```python
from jigen.integrations.llama_index import JigenVectorStore
from llama_index.core import VectorStoreIndex

store = JigenVectorStore(context.collection("knowledge"))
index = VectorStoreIndex.from_vector_store(store)
```

LlamaIndex supplies node embeddings to Jigen; `stores_text=True` lets an index
be reconstructed directly from the vector store.

## Content compatibility

The default serializer is contractless MessagePack, matching `Jigen.Client`.
Python dictionaries, dataclasses and Pydantic models are supported. A
`JsonSerializer` is also available when every writer and reader of a collection
uses JSON.

Python integer keys are encoded as little-endian signed 64-bit values. Strings,
UUIDs and raw bytes match the .NET `VectorKey` conversions. For a .NET 32-bit
integer key, pass its four-byte little-endian representation explicitly.

