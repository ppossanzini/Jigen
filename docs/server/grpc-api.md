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
| `CalculateImageEmbedding` | `ImageEmbeddingRequest` | `EmbeddingResponse` | Compute the embedding of an image from its raw bytes. Requires `ImagesModelPath` configured (vision model). |
| `CalculateImageEmbeddingBatch` | `ImageEmbeddingBatchRequest` | `EmbeddingBatchResponse` | Compute embeddings for many images in one call; results keep the input order (empty images yield an empty row). |
| `CalculateImageTileEmbeddings` | `ImageEmbeddingRequest` | `ImageTileEmbeddingsResponse` | Compute tile embeddings of an image: overlapping tiles each embedded separately, plus the whole-image embedding as the last entry. |
| `CreateMetadata` | `CreateMetadataRequest` | `CreateMetadataResponse` | Create a shared metadata record. Returns the opaque `MetadataIndex`. |
| `UpdateMetadata` | `UpdateMetadataRequest` | `UpdateMetadataResponse` | Replace the content of a shared metadata record; `Updated` is false when the index is unknown. |
| `GetEmbeddingMetadata` | `EmbeddingMetadataRequest` | `EmbeddingMetadataResponse` | Read the metadata index associated to an embedding key; `-1` when the key is absent or not associated. |
| `AssociateEmbedding` | `AssociateEmbeddingRequest` | `Result` | Associate or reassociate an existing embedding key to a metadata record. |
| `SearchByMetadata` | `SearchByMetadataRequest` | `SearchByMetadataResponse` | Grouped search: one result per metadata record, best score wins, `CandidateBudget` mandatory. |
| `GetSparse` | `ItemKey` | `SparseVector` | Read the sparse vector of a key; an empty message when the entry has none. |
| `SearchSparse` | `SearchSparseRequest` | `SearchVectorResponse` | Exact lexical search over caller-supplied term weights. |
| `SearchSparseByMetadata` | `SearchSparseByMetadataRequest` | `SearchSparseByMetadataResponse` | Grouped lexical search, one result per metadata record, `CandidateBudget` mandatory and greater than zero. |
| `SearchHybrid` | `SearchHybridRequest` | `SearchVectorResponse` | Dense search, lexical search and their fusion by position. |

`IngestResult.Accepted` counts the entries accepted into the server's ingestion pipeline; durability follows the server's group-commit policy, exactly like the unary `Set` operations.

`SetDocument`, `SetDocuments` and `SearchDocument` require an embedding module reachable from the server process (in-process on the all-in-one image, or remote over RabbitMQ from a `jigendb` server) — see [embeddings overview](../embeddings/overview.md). The image RPCs additionally require the vision model to be configured (`JigenEmbeddings:ImagesModelPath`); without it the server runs text-only and image RPCs fail with a clear configuration error.

### Sparse (lexical) slot

`Vector` and `Document` carry an optional `Sparse` field (tag 6 — tag 5 stays the
dense `Embeddings` and is never reused), so one write stores both vectors and the
two slots always describe the same revision of the entry.

```proto
message SparseVector {
  repeated int32 Terms = 1;
  repeated float Weights = 2;
}
```

Terms are unique, strictly increasing and non-negative; weights are finite,
non-negative with at least one above zero. An **absent** message means "no
lexical slot"; an **empty** one is refused with `InvalidArgument`. A request with
more terms than the server's configured cap (`SparseMaxTerms`) is refused too.

`SearchHybridRequest` takes `DenseWeight` and `SparseWeight` as `optional`: when
both vectors are supplied both must be present, a zero weight excludes that
modality, and both at zero is `InvalidArgument`. With a single vector the missing
weight defaults to one.

`SearchVectorResult` carries the provenance of every hit: `Score` is the fused
score on a hybrid search, `DenseScore`/`SparseScore` keep the original modality
scores, and `InDense`/`InSparse` report which search found the entry.

`CollectionInfoResponse` reports the term index in `Sparse`
(`terms`, `postings`, `averagePostingsPerTerm`, `memoryBytes`, `retiredTerms`,
`built`); the field is absent when the lexical index is disabled.

**Writes need the write-ahead log.** A write carrying terms with
`JigenServer:Wal:Enabled = false` is refused with `FailedPrecondition`: without
the log the slot could not be recovered after a crash. The log is on by default.

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

## Embedding RPCs

Text embedding messages:

```proto
message EmbeddingRequest {
  string Message = 1;
  string Task = 2;
}

message EmbeddingResponse {
  repeated float Embeddings = 1;
}

message EmbeddingBatchRequest {
  repeated string Messages = 1;
  string Task = 2;
}

message EmbeddingBatchResponse {
  // One entry per input message, same order. An input the model could not
  // embed yields an entry with an empty Embeddings list.
  repeated EmbeddingResponse Results = 1;
}
```

Image embedding messages (raw bytes in, the same `EmbeddingResponse` out — the vision model is aligned to the text embedding space, so image and text vectors are directly comparable):

```proto
message ImageEmbeddingRequest {
  bytes Image = 1;
}

message ImageEmbeddingBatchRequest {
  repeated bytes Images = 1;
}

// One EmbeddingResponse per tile (raster order) plus the whole-image embedding
// appended as the last entry.
message ImageTileEmbeddingsResponse {
  repeated EmbeddingResponse Tiles = 1;
}
```

Image and text embedding vectors can be stored together in a collection and searched cross-modally — see [embeddings overview](../embeddings/overview.md#image-embeddings).

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

## Shared metadata

A metadata record is content shared by several embeddings — the parent passage behind two chunk vectors, or an article indexed by both its title and its body. The content is global to the **database**; the association is per **collection and embedding key**. Many embeddings reference one record, and updating the record once changes what every associated embedding reads.

`MetadataIndex` is an opaque `int32` assigned by the server. It is stable across reopen and compaction, never reused after retirement, and `-1` always means "no association".

```proto
message CreateMetadataRequest {
  string Database = 1;
  bytes Content = 2;
}

message CreateMetadataResponse {
  int32 MetadataIndex = 1;
}

message UpdateMetadataRequest {
  string Database = 1;
  int32 MetadataIndex = 2;
  bytes Content = 3;
}

message UpdateMetadataResponse {
  bool Updated = 1;  // false when the metadata index does not exist
}

message EmbeddingMetadataRequest {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
}

message EmbeddingMetadataResponse {
  int32 MetadataIndex = 1;  // -1 when the key is absent or not associated
}

message AssociateEmbeddingRequest {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
  int32 MetadataIndex = 4;
}

message SearchByMetadataRequest {
  string Database = 1;
  string Collection = 2;
  repeated float Embeddings = 3;
  int32 Top = 4;
  // Mandatory: caps the sum of requested candidates across attempts,
  // repeated candidates included. 0 is rejected.
  int32 CandidateBudget = 5;
  FilterNode Filter = 6;
  SearchTuning Tuning = 7;
  // Used only when Embeddings is empty: the server calculates the query vector.
  string Sentence = 8;
}

message SearchByMetadataResult {
  int32 MetadataIndex = 1;
  bytes Key = 2;      // winning embedding of the group
  bytes Content = 3;  // empty with NoContent
  float Score = 4;    // highest score observed in the group
}

message SearchByMetadataResponse {
  repeated SearchByMetadataResult Results = 1;
}
```

`Content` is the raw serialized content, exactly the bytes `GetContent` returns, so a metadata record and a document payload stay interchangeable.

Behaviour of the surrounding operations:

- `GetContent` on an associated key returns the **shared** content, not a per-vector copy.
- `SetVector`/`SetDocument` and the streaming bulk variants on an already associated key update the shared record; every associated embedding sees the change.
- `DeleteVector` and `Clear` make the key read as absent. The record is not dropped — not even by compaction — and can be associated again.
- A write is visible when the call returns; durability follows the store's own save policy.
- `SearchByMetadata` ranks groups by the highest observed embedding score. Ties are ordered by metadata index. Results are shaped like `SearchVectorResponse` items, plus the group identity.
- `CandidateBudget` is bound per query and rejects `0`. When the budget is exhausted **fewer than `Top` groups may be returned**, and approximate indexes make **no promise of exact grouped top-k** — raise `EfSearch` or the budget to improve recall.
- The predicate in `SearchByMetadataRequest.Filter` uses the same [filter AST](#filter-ast) as `SearchDocumentRequest.Filter`.
- Rejections raised by the server (missing embedding key, unknown metadata index, non-positive `Top` or `CandidateBudget`, neither `Embeddings` nor `Sentence`) surface as `InvalidArgument`.

Grouped search is single-collection. Bulk ingestion attached to a shared record, transactions over metadata and cross-modal grouped search are not part of this contract.

## Error handling

Server-side failures surface to callers as standard gRPC errors (`RpcException` in .NET clients).

The codebase also ships an optional richer mechanism — a server interceptor that attaches an `exception-bin` trailer with a safe payload (original exception type name, message, detail), and a matching client interceptor that rethrows it as a typed exception (`JigenServerException` with `ServerExceptionType` when the original type cannot be reconstructed). **In the current build both interceptors are disabled by default** (commented out in `Jigen.Grpc.Module` and `Jigen.Client.Context`), so expect plain `RpcException`s unless you re-enable them.
