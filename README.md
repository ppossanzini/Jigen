# Jigen DB

**Jigen DB** is a vector database written from scratch in C#.
It is a study/research project focused on vector search use cases and implementation trade-offs.

The goal is to iteratively explore performance strategies, indexing approaches, and practical optimizations.


> Tech stack: **.NET (net8.0 / net10.0)**, **ASP.NET Core** (hosting), **gRPC**.

---

## Table of contents

- [Jigen DB](#jigen-db)
  - [Table of contents](#table-of-contents)
  - [Prerequisites](#prerequisites)
  - [High-level structure](#high-level-structure)
  - [Running the gRPC server](#running-the-grpc-server)
  - [Using the client](#using-the-client)
  - [CORS / gRPC-Web](#cors--grpc-web)
  - [Tests](#tests)
  - [Troubleshooting](#troubleshooting)
    - [HNSW index file (`*.hnsw.index`) is empty](#hnsw-index-file-hnswindex-is-empty)
    - [HNSW returns fewer results than brute-force](#hnsw-returns-fewer-results-than-brute-force)
    - [gRPC client cannot connect](#grpc-client-cannot-connect)
  - [License](#license)

---

## Prerequisites

- A .NET SDK compatible with the project targets (net8.0 and/or net10.0)
- Read/write access to the local database folder used by the server
- Optional ONNX model files if you want to use server-side embedding generation

Check your installation:

```bash
dotnet --info
```

## High-level structure

- **Server**: [src/Server/Jigen](src/Server/Jigen) hosts the app and loads modules.
- **gRPC module**: [src/Server/Jigen.Grpc](src/Server/Jigen.Grpc) exposes `StoreCollectionService`.
- **Store engine**: [src/Jigen/Jigen.Store](src/Jigen/Jigen.Store) handles storage, indexing and search.
- **HNSW indexer**: [src/Jigen/Jigen.Indexer.HNSW](src/Jigen/Jigen.Indexer.HNSW).
- **Client library**: [src/Client/Jigen.Client](src/Client/Jigen.Client).
- **Tests**: [tests](tests) with integration/unit suites for store, client and HNSW.


## Running the gRPC server

Run the host project:

```bash
dotnet run --project src/Server/Jigen/Jigen.csproj
```

Default endpoints are configured in `src/Server/Jigen/Program.cs`:

- `http://localhost:13223` (`Http1AndHttp2`)
- `http://localhost:3223` (`Http2`, gRPC)

The gRPC service is mapped by the module in [src/Server/Jigen.Grpc/Module.cs](src/Server/Jigen.Grpc/Module.cs).


## Using the client

The client package is in [src/Client/Jigen.Client](src/Client/Jigen.Client).

Minimal usage pattern:

```csharp
using Jigen.Client;
using Jigen.Client.BaseTypes;

var ctx = new Context(new ConnectionOptions
{
	HostName = "localhost",
	Port = 3223,
	TLS = false,
	DatabaseName = "Test"
});

var collection = new VectorCollection<MyDocument>(ctx);

collection.Add(1, new VectorEntry<MyDocument>
{
	Key = 1,
	Content = new MyDocument { Id = Guid.NewGuid(), Text = "hello" },
	Embedding = Array.Empty<float>()
});
```

For a concrete wrapper pattern, see [tests/JigenClientTest/Model/DB.cs](tests/JigenClientTest/Model/DB.cs) and [tests/JigenClientTest/UnitTest1.cs](tests/JigenClientTest/UnitTest1.cs).


## CORS / gRPC-Web

- General server CORS is configured in [src/Server/Jigen/Program.cs](src/Server/Jigen/Program.cs).
- gRPC-specific CORS policy is configured in [src/Server/Jigen.Grpc/Module.cs](src/Server/Jigen.Grpc/Module.cs).

Current status:

- Native gRPC is enabled (`MapGrpcService<Server>()`).
- gRPC-Web mapping is currently commented out in the module:
  - `.EnableGrpcWeb()`
  - `.RequireCors(JigenGrpcCorsDefaultPolicy)`

If you need browser gRPC-Web clients, enable those lines and verify CORS policy for your frontend origin.


## Tests

Run all tests:

```bash
dotnet test Jigen.sln
```

Run focused suites:

```bash
dotnet test tests/HnswTest/HnswTest.csproj
dotnet test tests/JigenStoreTests/JigenStoreTests.csproj
dotnet test tests/JigenClientTest/JigenClientTest.csproj
dotnet test tests/PrimitiveTests/PrimitiveTests.csproj
```


## Troubleshooting

### HNSW index file (`*.hnsw.index`) is empty

If the `*.hnsw.index` file appears empty and HNSW returns no results after restart, ensure your app calls:

1. `SaveChangesAsync()`
2. `Close()`

Recent fixes added an explicit indexer flush during store close, so `StoredList` header/index are persisted before process exit.

### HNSW returns fewer results than brute-force

This is expected in principle because HNSW is ANN (approximate nearest neighbors), but large gaps usually indicate:

- too-low `EfSearch`
- insufficient `EfConstruction`
- graph quality issues (construction/traversal bugs)

Tune `EfSearch` and `EfConstruction` in `SmallWorldOptions` first.

### gRPC client cannot connect

Check:

1. server is running
2. client uses `http://localhost:5001` for HTTP/2 gRPC
3. firewall/port blocks are not present


## License

See [LICENSE.txt](LICENSE.txt).

