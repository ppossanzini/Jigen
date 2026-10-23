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

