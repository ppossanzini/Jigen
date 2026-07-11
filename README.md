<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/jigen-logo-full-dark.png">
    <img src="assets/jigen-logo-full.png" alt="Jigen DB" width="320"/>
  </picture>
</p>

# Jigen DB

**Jigen DB** is a vector database written from scratch in C# for the .NET platform.

Use it **in-process** inside your application — like SQLite, but for vector search — or run it as a **standalone server** with gRPC and REST APIs, typed .NET client, and optional built-in text embedding generation powered by ONNX Runtime.

> Tech stack: **.NET (net8.0 / net10.0)**, **ASP.NET Core**, **gRPC**, **ONNX Runtime**. License: **Apache-2.0**.

## Project Overview and Genesis

This project was born as a research initiative to understand the inner workings of vector databases and to explore how performant a database written in pure C# can be. It also provided a great opportunity to dive deeper into specific C# and .NET runtime internals.

**Architecture & Development**

  - Core Storage & Persistence: Written entirely from scratch by human developer.
  - HNSW Indexing: Forked from a Microsoft library and heavily optimized/modified to support disk persistence.
  - Server Architecture: Built using a well-known CQRS pattern, written by human. 
  - This software has been optimized using Fable.  
  - AI-Generated UI: The frontend was written completely by AI. Since UI code isn't the focus of this project, a functional, AI-built interface was more than enough.
  - Testing & Debugging: Handled entirely by a human.
  - Messaging & IPC (Hikyaku): The communication layer uses Hikyaku, a custom fork of MediatR (v12) developed over several years. It introduces out-of-process capabilities via Kafka and RabbitMQ. (Note: The library was previously named Arbitrer, then Axonflow, and finally rebranded to Hikyaku to avoid naming conflicts on NuGet).



## Highlights

- **In-process engine** (`Jigen.Store`): append-only memory-mapped storage, asynchronous ingestion, crash recovery, exact (brute-force) search out of the box.
- **HNSW index** (`Jigen.Indexer.HNSW`): disk-backed approximate nearest neighbor graph with concurrent inserts, deletes, optional SQ8 quantization and exact reranking.
- **Server**: multi-database host with gRPC (port 3223) and REST (port 13223) APIs, per-collection search with content filters, periodic durability checkpoints.
- **Embeddings**: server-side text embedding generation (ONNX), in-process or scaled out to dedicated worker containers over RabbitMQ; CPU by default, GPU execution providers available.
- **Typed .NET client** (`Jigen.Client`): dictionary-like collections, LINQ predicates translated to server-side filters.

## Installation

**NuGet** (in-process):

```bash
dotnet add package Jigen.Store          # embedded engine (net10.0)
dotnet add package Jigen.Indexer.HNSW   # ANN index for the engine
```

**NuGet** (client):

```bash
dotnet add package Jigen.Client         # client for the server (net8.0+)
```

**Docker** (server):

```bash
# all-in-one: database + embedding generation in a single container
docker run -d -p 3223:3223 -p 13223:13223 \
  -v jigen-data:/data/jigendb -v ./models:/data/onnx \
  ppossanzini/jigendb-all-in-one
```

Three images are published: `ppossanzini/jigendb` (server only), `ppossanzini/jigendb-all-in-one` (server + embeddings) and `ppossanzini/jigen-embeddings` (embedding worker). See [Docker deployment](docs/server/docker.md).

## Quick taste

In-process:

```csharp
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;

using var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo",
  Indexer = new SmallWorldIndexer(new SmallWorldOptions(
    m: 16, efConstruction: 200, efSearch: 64, storagePath: "/data/jigendb/hnsw"))
});

await store.AppendContent(new VectorEntry
{
  Id = Guid.NewGuid().ToByteArray(),
  CollectionName = "articles",
  Content = MessagePackDocumentSerializer.Instance.Serialize("hello vectors"),
  Embedding = embedding                       // float[] from your embedding model
});

var results = store.Search("articles", queryEmbedding, top: 10);
await store.SaveChangesAsync();
```

Client against a server:

```csharp
using Jigen.Client;

var ctx = new Context(new ConnectionOptions { HostName = "localhost", Port = 3223, DatabaseName = "demo" });
var articles = new VectorCollection<Article>(ctx);

articles.Add(Guid.NewGuid(), new Article { Title = "..." }, sentence: "text embedded by the server");
var hits = articles.Search("query text", x => x.Category == "news", top: 5);
```

## Documentation

Full documentation lives in [`docs/`](docs/index.md) (also buildable with MkDocs / Read the Docs).

| Section | Contents |
|---|---|
| [In-process engine](docs/in-process/overview.md) | [Getting started](docs/in-process/getting-started.md) · [Store options](docs/in-process/store-options.md) · [Collections](docs/in-process/collections.md) |
| [Indexes](docs/indexes/hnsw.md) | [Brute force](docs/indexes/brute-force.md) · [HNSW](docs/indexes/hnsw.md) |
| [Embeddings](docs/embeddings/overview.md) | [Configuration](docs/embeddings/configuration.md) · [Execution providers (CPU/GPU)](docs/embeddings/execution-providers.md) |
| [Server](docs/server/overview.md) | [Configuration](docs/server/configuration.md) · [Docker](docs/server/docker.md) · [REST API](docs/server/rest-api.md) · [gRPC API](docs/server/grpc-api.md) |
| [Client](docs/client/getting-started.md) | [Usage](docs/client/usage.md) |
| [Benchmarks & hardware](docs/benchmarks.md) | Current numbers, supported and upcoming CPU/GPU technologies |

The server also ships a web administration UI (Jigen Insight) served on port 13223; it is not covered by this documentation yet.

## Building from source

```bash
dotnet build Jigen.sln -m:1        # -m:1 required (StaticWebAssets breaks parallel builds)
dotnet run --project src/Server/Jigen/Jigen.csproj
```

Tests:

```bash
dotnet test tests/JigenStoreTests/JigenStoreTests.csproj
dotnet test tests/HnswTest/HnswTest.csproj
dotnet test tests/PrimitiveTests/PrimitiveTests.csproj
# tests/JigenClientTest requires a running server on localhost:3223
```

## License

Apache-2.0 — see [LICENSE.txt](LICENSE.txt).
