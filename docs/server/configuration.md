# Server configuration

The server reads its configuration from `appsettings.json` plus the standard ASP.NET Core configuration providers, so every setting below can also be supplied as an environment variable using the double-underscore convention, e.g. `JigenServer__Https__Mode=Random` or `JigenServer__Index__M=32`.

## `JigenServer`

Applies to the server host and to every database it opens.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `DataFolderPath` | string | *(required)* | Root folder where database files are stored (`{database}.content.jigen`, `{database}.vectors.jigen`, ... directly in this folder), plus one `hnsw/{database}/` graph folder per database. |
| `MemoryLimitMB` | int | `2048` | Advisory memory budget for the server process. |
| `CheckpointIntervalSeconds` | int | `30` | Seconds between durability checkpoints: every open database gets a `SaveChangesAsync` (fsync of content/embeddings/index, graph flush), which also surfaces background ingestion errors in the logs. `0` disables the periodic checkpoint (a checkpoint still runs on shutdown). |
| `IndexerWorkers` | int | `0` | Background indexing workers per database. `0` = automatic (clamp of CPU/2, between 1 and 8). |
| `ReconcileOnUncleanShutdown` | bool | `true` | Reconciles the vector index with the store content when a database was not closed cleanly (crash recovery). |
| `Https:Mode` | string | `None` | `None` (plain HTTP on 13223), `Random` (self-signed certificate generated at startup), or `FromFile` (load `CertificatePath`/`CertificatePassword`). |
| `Https:CertificatePath` | string | `""` | Path to a certificate file; required when `Mode` is `FromFile`. |
| `Https:CertificatePassword` | string | `""` | Password for the certificate file, if any. |

Port 3223 (gRPC) is always plaintext HTTP/2; `Https` only affects port 13223.

## `JigenServer:Index`

HNSW parameters applied to every database opened by this server instance (passed to `SmallWorldOptions` when the server creates each database's indexer). See [HNSW index](../indexes/hnsw.md) for what each parameter controls.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `M` | int | `16` | Max connections per node per layer (2M on layer 0). |
| `EfConstruction` | int | `200` | Construction beam width (build quality vs. ingest speed). |
| `EfSearch` | int | `50` | Search beam width (recall vs. latency; raise it on large collections). |
| `Sq8Quantization` | bool | `false` | SQ8-quantizes the graph vectors (4x smaller graph files, less memory bandwidth); store embeddings stay full precision. Applies to newly written graph records. |
| `ExactRerank` | bool | `true` | With SQ8 enabled, rescore candidates with full-precision embeddings from the store before returning results. |
| `LazyHnswThreshold` | int | `0` | When greater than zero, ingestion skips HNSW graph construction (pure file writes) until the total number of stored vectors across all collections meets or exceeds this count, at which point the graph is built from the store in a single reconciliation pass. Set to `100000` or higher for bulk-load scenarios to maximise initial ingestion throughput. `0` disables the lazy behaviour and builds the graph eagerly. |

## `JigenEmbeddings:Tasks`

```json
"JigenEmbeddings": {
  "Tasks": ["search_document", "search_query", "clustering", "classification"]
}
```

The list of task prefixes advertised by `GET /api/embeddings/tasks` (see [REST API](rest-api.md)). The all-in-one and embedding-worker images additionally read the full `JigenEmbeddings` settings (tokenizer/model paths, generator options) described in [embeddings configuration](../embeddings/configuration.md); the plain `jigendb` image only needs the `Tasks` list since it never runs the ONNX pipeline itself.

## `Kaido` and `RabbitMQ` (distributed topology)

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Kaido:Enabled` | bool | `false` | Enables remote dispatch of embedding requests through the Hikyaku/Kaido mediator over RabbitMQ. Required on the `jigendb` (non-all-in-one) image for `SearchDocument`/`SetDocument`/`/api/embeddings` to work, since that image has no embedding module of its own. |
| `RabbitMQ:HostName` | string | `localhost` | RabbitMQ broker host. |
| `RabbitMQ:Port` | int | `5672` | RabbitMQ broker port. |
| `RabbitMQ:ExchangeName` | string | `JigenExchange` | Exchange used for Kaido message dispatch. |
| `RabbitMQ:UserName` | string | `jigen` | Broker username. |
| `RabbitMQ:Password` | string | `P@ssw0rd` | Broker password. Change this in any non-local deployment. |
| `RabbitMQ:VirtualHost` | string | `Jigen` | Broker virtual host. |
| `RabbitMQ:PerChannelQos` | int | `10` | Per-channel prefetch count. |
| `RabbitMQ:PerConsumerQos` | int | `10` | Per-consumer prefetch count. |

See [Docker](docker.md) for a compose example wiring these settings between the `jigendb` server and `jigen-embeddings` workers.

## `JigenIdentity`

The server ships with an identity module that seeds a default administrative user and OAuth-style client on first run (`JigenIdentity:SeedUser`, `JigenIdentity:DefaultClient` in `appsettings.json`). This module backs both the REST/gRPC authorization and the Jigen Insight web admin UI, which is out of scope for this documentation. **Change the default seed user credentials before exposing a server outside a trusted network** — the shipped defaults are for local evaluation only.

## Example: distributed topology, database server

```json
{
  "JigenServer": {
    "DataFolderPath": "/data/jigendb",
    "MemoryLimitMB": 4096,
    "CheckpointIntervalSeconds": 30,
    "Index": { "M": 16, "EfConstruction": 200, "EfSearch": 50 }
  },
  "Kaido": { "Enabled": true },
  "RabbitMQ": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "ExchangeName": "JigenExchange",
    "UserName": "jigen",
    "Password": "change-me",
    "VirtualHost": "Jigen"
  }
}
```

## Tuning: from defaults to production

The tables above list every parameter; this section explains how they *interact* and how to move from the shipped defaults to a configuration that fits your workload. The three levers that matter most for search performance are the HNSW knobs `M`, `EfConstruction` and `EfSearch` — see [What is HNSW](../concepts/hnsw.md) for the algorithm, and [HNSW index](../indexes/hnsw.md) for the full reference.

### 1. Understand the phases you are tuning

| Phase | Knobs | Cost of getting it wrong |
|---|---|---|
| Ingestion | `LazyHnswThreshold`, `IndexerWorkers`, `EfConstruction` | Slow initial load, CPU saturation during bulk import |
| Memory footprint | `MemoryLimitMB`, `Sq8Quantization`, `M` | OOM, swapping, or unnecessary disk/memory waste |
| Search latency | `EfSearch` (per-query too), `M` | Slow queries or poor recall |
| Recall quality | `EfSearch`, `EfConstruction`, `M`, `ExactRerank` | Results that miss the true nearest neighbors |

Because `EfSearch` is a **query-time** parameter, it can also be overridden per request (the gRPC `SearchTuning.EfSearch`, and the client's per-query tuning) without touching the server configuration — the fastest way to experiment.

### 2. Ingestion: bulk loading vs. steady state

For a one-off bulk import (millions of vectors), graph construction is the bottleneck: every insert runs a search to find its neighbors. Two settings help:

- **`LazyHnswThreshold`** — when the expected final size is known, set it to a value slightly below the expected vector count (e.g. `100000`). Ingestion then writes vectors to the store **without building the graph**, and the graph is built in a single reconciliation pass once the threshold is reached. This maximizes throughput during the load; the trade-off is that searches during the load run a full scan (or the store's fallback), and the final graph build takes time.
- **`IndexerWorkers`** — during steady-state ingestion, more workers build the graph in parallel. The default (`0` = automatic, CPU/2 clamped to 1–8) is usually right; raising it competes with ONNX inference threads on an all-in-one image (see step 5).

For **steady-state** ingestion (a trickle of new documents), leave `LazyHnswThreshold` at `0` — incremental insertion is exactly what HNSW is designed for.

### 3. Latency vs. recall: where the real trade-off lives

The cheapest lever is `EfSearch` (query-time, no rebuild):

- **Latency-bound** (search-as-you-type, high QPS): lower `EfSearch` (e.g. `30–40`). Recall drops, but queries get faster and cheaper.
- **Recall-bound** (RAG, retrieval where quality matters): raise `EfSearch` (e.g. `100–200`). The graph is untouched, so this is a zero-cost experiment.
- **Still not enough recall?** Raise `M` and `EfConstruction` — but this changes the stored graph: existing databases need the index rebuilt (or grow the graph over time as new inserts re-wire connections).

A practical workflow:

1. Start with the defaults (`M: 16`, `EfConstruction: 200`, `EfSearch: 50`).
2. Measure recall against brute force on a sample (Jigen ships benchmarks for this — see [benchmarks](../benchmarks.md)).
3. If recall is the problem, raise `EfSearch` first; re-measure.
4. Only if that is insufficient, raise `M`/`EfConstruction` (accepting rebuild/growth cost).
5. If latency is the problem, lower `EfSearch`, and only then consider quantization or a smaller `M`.

### 4. Memory: quantization is the biggest lever

- **`Sq8Quantization: true`** shrinks the graph's vector file ~4× (float → int8) and roughly halves memory bandwidth during traversal — often a *faster* config in addition to a smaller one.
- **`ExactRerank: true`** (the default) recovers the precision lost to quantization by rescoring the final candidates against the store's full-precision embeddings, so the recall impact is small. Disable it only when you accept the recall drop and want the last bit of latency.
- **`M`** directly sizes the adjacency lists: halving `M` roughly halves the graph's adjacency memory.
- **`MemoryLimitMB`** is an advisory budget for the whole process — on an all-in-one image it must cover the ONNX models too, not just the graphs.

### 5. Embeddings interplay (all-in-one / worker images)

Embedding inference and graph construction compete for the same CPU. On an all-in-one image:

- The embeddings queue settings (`EmbeddingsMaxConcurrency`, `EmbeddingsQueueCapacity`, `EmbeddingsQueueTimeoutSeconds`, `MaxBatchSize`) bound how many ONNX runs execute at once — see [embeddings configuration](../embeddings/configuration.md).
- During a large ingestion, both `IndexerWorkers` and embedding workers will be active; if ingestion is ONNX-heavy (server-side `SetDocuments`), prefer keeping `IndexerWorkers` at the automatic default rather than raising it.
- If ingestion and search must coexist with predictable latency, consider the distributed topology (`Kaido:Enabled`) so embedding work runs on separate worker processes.

### 6. When the defaults are fine

If your collections are small (up to a few hundred thousand vectors) and queries are interactive, the **defaults are already right** — `M: 16`, `EfConstruction: 200`, `EfSearch: 50` with brute force as the fallback and no quantization. The tuning above is for collections large enough that scan cost or graph size actually matters.
