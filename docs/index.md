<p align="center">
  <!-- Light variant only: the Read the Docs theme always renders on a light page. -->
  <img src="assets/jigen-logo-full.png" alt="Jigen DB" width="320"/>
</p>

# Jigen DB Documentation

**Jigen DB** is a vector database written from scratch in C# for the .NET platform. It can run **in-process** inside your application (like SQLite, but for vectors) or as a **standalone server** exposing gRPC and REST APIs, with optional built-in text embedding generation based on ONNX Runtime.

## Where to start

- Embedding vectors in your own .NET application → [In-process engine](in-process/getting-started.md)
- Running a database server (Docker) → [Server & Docker](server/docker.md)
- Connecting to a server from .NET → [Client](client/getting-started.md)

## Contents

### In-process engine

- [Overview](in-process/overview.md) — architecture, storage model, durability
- [Getting started](in-process/getting-started.md) — install from NuGet, first database
- [Store options](in-process/store-options.md) — all `StoreOptions` parameters, files on disk, crash recovery
- [Collections](in-process/collections.md) — `VectorCollection<T>`, `DocumentCollection<T>`, keys, serialization, filtering

### Indexes

- [Brute-force index](indexes/brute-force.md) — exact search, the default
- [HNSW index](indexes/hnsw.md) — approximate nearest neighbor, all `SmallWorldOptions` parameters, SQ8 quantization, tuning

### Embeddings

- [Overview](embeddings/overview.md) — ONNX pipeline, chunking, deployment options
- [Configuration](embeddings/configuration.md) — all generator and service parameters
- [Execution providers (CPU/GPU)](embeddings/execution-providers.md) — CUDA, DirectML, OpenVINO, CoreML, ROCm

### Server

- [Overview](server/overview.md) — architecture, modules, deployment topologies
- [Configuration](server/configuration.md) — all `appsettings.json` / environment parameters
- [Docker](server/docker.md) — the three container images, compose examples
- [REST API](server/rest-api.md) — endpoints and examples
- [gRPC API](server/grpc-api.md) — service contract and messages

### Client

- [Getting started](client/getting-started.md) — install, connection options
- [Usage](client/usage.md) — inserting, searching, filtering

### Reference

- [Benchmarks & hardware support](benchmarks.md) — current numbers, supported and upcoming CPU/GPU technologies

## Packages and images

| Artifact | Type | Purpose |
|---|---|---|
| [`Jigen.Store`](https://www.nuget.org/packages/Jigen.Store) | NuGet (net10.0) | In-process vector database engine |
| [`Jigen.Indexer.HNSW`](https://www.nuget.org/packages/Jigen.Indexer.HNSW) | NuGet (net10.0) | HNSW index for the store |
| [`Jigen.Client`](https://www.nuget.org/packages/Jigen.Client) | NuGet (net8.0) | gRPC client for the Jigen server |
| [`ppossanzini/jigendb`](https://hub.docker.com/r/ppossanzini/jigendb) | Docker | Database server (embeddings delegated to workers) |
| [`ppossanzini/jigendb-all-in-one`](https://hub.docker.com/r/ppossanzini/jigendb-all-in-one) | Docker | Database server with in-process embedding generation |
| [`ppossanzini/jigen-embeddings`](https://hub.docker.com/r/ppossanzini/jigen-embeddings) | Docker | Standalone embedding worker |

Jigen DB is open source under the [Apache-2.0 license](https://github.com/ppossanzini/Jigen/blob/main/LICENSE.txt).
