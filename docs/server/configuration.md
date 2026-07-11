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
