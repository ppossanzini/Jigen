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

## Shared metadata

One metadata record can back several embeddings. The record is database-scoped
and addressed by an opaque index; the association is per collection and key.
Reading an associated key returns the shared content, so updating the record
once updates every embedding that points at it.

```python
passage = articles.create_metadata(
    {"title": "Vector search", "body": "A practical introduction to vector search in .NET."}
)

articles.upsert("chunk-1", {"text": "first chunk"}, embedding=chunk_1_vector)
articles.upsert("chunk-2", {"text": "second chunk"}, embedding=chunk_2_vector)
articles.associate_metadata("chunk-1", passage)
articles.associate_metadata("chunk-2", passage)

articles.get_metadata_index("chunk-1")   # -> passage; -1 when not associated
articles.update_metadata(passage, {"title": "Vector search", "body": "Revised once, read by both chunks."})
```

Grouped search returns one result per metadata record, ranked by the best score
observed among its embeddings. `candidate_budget` is keyword-only and
mandatory, because it is what bounds the work the server may do to complete
groups:

```python
groups = articles.search_by_metadata(
    "vector database for .NET",
    top=10,
    candidate_budget=400,
    filter=equals("category", "guide"),
    options=SearchOptions(ef_search=200),
)

for group in groups:
    print(group.metadata_index, group.score, group.content)
```

`MetadataSearchResult` exposes `metadata_index`, `key`, `content` and `score`.
Iterables of vectors work as the query too (`search_by_metadata(chunk_vector, ...)`).
Fewer than `top` groups may come back when the budget is exhausted, and
approximate indexes make no promise of exact grouped top-k. A non-positive
`top` or `candidate_budget` raises `ValueError` before any call. The async
client exposes the same methods as coroutines.

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

