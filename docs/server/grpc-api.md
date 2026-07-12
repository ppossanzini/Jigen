# gRPC API

The gRPC service is served on port `3223` as plaintext HTTP/2 by default (see [server overview](overview.md); enable TLS in front of it with a reverse proxy or the client's TLS option, see [client getting started](../client/getting-started.md)).

Service: `jigen.StoreCollectionService` (`csharp_namespace = Jigen.Proto`).

## Methods

| Method | Request | Response | Description |
|---|---|---|---|
| `ListCollections` | `CollectionKey` | `ListCollectionResult` | List collection names in a database. |
| `GetContent` | `ItemKey` | `RawContentResult` | Get the raw serialized content for a key. |
| `GetEmbedding` | `ItemKey` | `EmbeddingResponse` | Get the stored full-precision embedding of a key (empty when the key is missing or the entry has no vector). |
| `SetDocument` | `Document` | `Result` | Insert/update a document from a raw `Sentence`; the server computes the embedding via its embedding module. |
| `SetVector` | `Vector` | `Result` | Insert/update a document with a precomputed embedding. |
| `SetVectors` | `stream Vector` | `IngestResult` | Bulk insert with precomputed embeddings over a single client-streaming call: one round trip for the whole batch. |
| `SetDocuments` | `stream Document` | `IngestResult` | Bulk insert from raw `Sentence`s; the server batches the embedding calculation (windows of 64 sentences per embedding dispatch). |
| `SearchVector` | `SearchVectorRequest` | `SearchVectorResponse` | Search by a precomputed query vector, optionally with a filter and per-query tuning. |
| `SearchDocument` | `SearchDocumentRequest` | `SearchVectorResponse` | Search by a raw `Sentence`; the server computes the embedding and optionally applies a filter and per-query tuning. |
| `DeleteVector` | `ItemKey` | `Result` | Delete a document/vector by key. |
| `GetAllKeys` | `CollectionKey` | `KeysResult` | List all keys in a collection (single message — prefer `StreamKeys` on large collections). |
| `StreamKeys` | `StreamKeysRequest` | `stream KeysResult` | Stream all keys in chunks (default 1000 per message); safe at any collection size. |
| `Contains` | `ItemKey` | `Result` | Check whether a key exists. |
| `Clear` | `CollectionKey` | `Result` | Remove all documents from a collection. |
| `Count` | `CollectionKey` | `CountResult` | Count documents in a collection. |
| `CalculateEmbeddings` | `EmbeddingRequest` | `EmbeddingResponse` | Compute an embedding for a sentence, optionally with a task prefix. |
| `CalculateEmbeddingsBatch` | `EmbeddingBatchRequest` | `EmbeddingBatchResponse` | Compute embeddings for many sentences in one call; results keep the input order (blank inputs yield an empty row). |

`IngestResult.Accepted` counts the entries accepted into the server's ingestion pipeline; durability follows the server's group-commit policy, exactly like the unary `Set` operations.

`SetDocument`, `SetDocuments` and `SearchDocument` require an embedding module reachable from the server process (in-process on the all-in-one image, or remote over RabbitMQ from a `jigendb` server) — see [embeddings overview](../embeddings/overview.md).

## Proto excerpts

Keys, collections and database are addressed by name/bytes on every message:

```proto
message ItemKey{
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
}

message CollectionKey{
  string Database = 1;
  string Collection = 2;
}

message Document {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
  bytes Content = 4;
  string Sentence = 5;
}

message Vector {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
  bytes Content = 4;
  repeated float Embeddings = 5;
}
```

Search requests/responses:

```proto
message SearchVectorRequest {
  string Database = 1;
  string Collection = 2;
  repeated float Embeddings = 3;
  int32 Top = 4;
  FilterNode Filter = 5;
  SearchTuning Tuning = 6;
}

message SearchDocumentRequest {
  string Database = 1;
  string Collection = 2;
  string Sentence = 3;
  int32 Top = 4;
  FilterNode Filter = 5;
  SearchTuning Tuning = 6;
}

// Optional per-query tuning; zero values keep the server defaults.
message SearchTuning {
  int32 EfSearch = 1;         // HNSW beam width override (recall vs latency)
  bool NoContent = 2;         // return keys and scores only
  optional float MinScore = 3; // drop results below this similarity
}

message SearchVectorResult {
  bytes Key = 1;
  bytes Content = 2;
  float Score = 3;
}

message SearchVectorResponse {
  repeated SearchVectorResult Results = 1;
}
```

## Filter AST

`SearchDocumentRequest.Filter` carries the same predicate AST described in the [in-process filtering model](../in-process/collections.md), translated to protobuf messages by the client (see [client usage](../client/usage.md)):

```proto
message FilterNode {
  oneof Kind {
    PropertyEqualsCondition Equals = 1;
    PropertyCollectionAnyCondition CollectionAny = 2;
    LogicalCondition And = 3;
    LogicalCondition Or = 4;
  }
}

message PropertyEqualsCondition {
  string PropertyPath = 1;
  FilterValue Value = 2;
}

message PropertyCollectionAnyCondition {
  string PropertyPath = 1;
  FilterValue Value = 2;
}

message LogicalCondition {
  FilterNode Left = 1;
  FilterNode Right = 2;
}

message FilterValue {
  oneof Kind {
    string StringValue = 1;
    int32 IntValue = 2;
    int64 LongValue = 3;
    double DoubleValue = 4;
    bool BoolValue = 5;
    bool NullValue = 6;
  }
}
```

## Error handling

Server-side failures surface to callers as standard gRPC errors (`RpcException` in .NET clients).

The codebase also ships an optional richer mechanism — a server interceptor that attaches an `exception-bin` trailer with a safe payload (original exception type name, message, detail), and a matching client interceptor that rethrows it as a typed exception (`JigenServerException` with `ServerExceptionType` when the original type cannot be reconstructed). **In the current build both interceptors are disabled by default** (commented out in `Jigen.Grpc.Module` and `Jigen.Client.Context`), so expect plain `RpcException`s unless you re-enable them.
