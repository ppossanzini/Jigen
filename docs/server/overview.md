# Server overview

The Jigen server is an ASP.NET Core (net10.0) host that exposes a Jigen store over the network, so multiple clients and processes can share the same set of databases instead of each embedding the store in-process.

## Architecture

The server is composed of a fixed host (`Program.cs`) plus a set of modules discovered and wired up via MEF composition (`IModule` implementations, one per assembly):

- **Jigen.Grpc** — the gRPC service (`StoreCollectionService`) used for all document/vector CRUD and search.
- **Jigen.API** — the REST controllers (databases, collections, documents, search) plus the OpenAPI document and Scalar UI.
- **Jigen.Identity.API** / **Jigen.Identity.Handlers** — an identity module (seed user, OAuth-style client, login) providing authentication and per-database access checks; change the default credentials before exposing a server (see [configuration](configuration.md#jigenidentity)).
- **Jigen.Metrics.API** / **Jigen.Metrics.Handlers** — server status/metrics history.
- **Jigen.TextEmbedding.Api** / **Jigen.TextEmbedding.Handlers** — present only in the all-in-one build; adds in-process embedding generation and the `/api/embeddings` REST endpoint.

Commands and queries flow through a CQRS mediator (Hikyaku); when `Kaido:Enabled` is set, the same mediator can dispatch selected requests (embedding calculation) to remote workers over RabbitMQ instead of handling them locally — this is what lets the database server and the embedding workers be separate processes without the API or gRPC surface changing.

Each open database is a regular in-process `Jigen.Store` (see [in-process engine](../in-process/overview.md)) managed by a `DatabasesManager`: on startup it reopens every known database from `JigenServer:DataFolderPath`, and on creation it wires the HNSW indexer with the server-wide index settings (see [configuration](configuration.md)) into a per-database graph folder (`hnsw/{database}/`) so that same-named collections in different databases never collide.

## Deployment topologies

Jigen ships as two distinct topologies, backed by different Docker images (see [Docker](docker.md)):

| Topology | Images | Embedding generation |
|---|---|---|
| All-in-one | `ppossanzini/jigendb-all-in-one` (single container) | In-process ONNX Runtime, no RabbitMQ required |
| Distributed | `ppossanzini/jigendb` + one or more `ppossanzini/jigen-embeddings` + RabbitMQ | Delegated to remote worker(s) over RabbitMQ (Hikyaku/Kaido mediator) |

The distributed topology exists so that embedding generation (CPU/GPU-bound, ONNX inference) can be scaled independently of the database server (I/O-bound, index maintenance): add more `jigen-embeddings` replicas without touching the database container, and without every database server needing GPU access. The all-in-one topology trades that independent scaling for a simpler, single-container deployment.

Both images expose the same two ports and the same REST/gRPC surface; the difference is purely which module resolves embedding requests.

## Ports

| Port | Protocol | Purpose |
|---|---|---|
| `3223` | HTTP/2 (gRPC only) | `StoreCollectionService` — see [gRPC API](grpc-api.md) |
| `13223` | HTTP/1.1 + HTTP/2 | REST API, OpenAPI document, Scalar interactive reference (`/scalar`), and the web admin SPA — see [REST API](rest-api.md) |

HTTPS on port 13223 is optional and configured via `JigenServer:Https` (see [configuration](configuration.md)).

## Web administration UI

Port 13223 also serves a bundled single-page application, **Jigen Insight**, for browsing databases and collections through a browser. It is out of scope for this documentation beyond this mention.
