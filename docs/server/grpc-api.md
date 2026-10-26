# gRPC API

The gRPC service is served on port `3223` as plaintext HTTP/2 by default (see [server overview](overview.md); enable TLS in front of it with a reverse proxy or the client's TLS option, see [client getting started](../client/getting-started.md)).

Service: `jigen.StoreCollectionService` (`csharp_namespace = Jigen.Proto`).

## Methods

| Method | Request | Response | Description |
|---|---|---|---|
| `ListCollections` | `CollectionKey` | `ListCollectionResult` | List collection names in a database. |
| `GetContent` | `ItemKey` | `RawContentResult` | Get the raw serialized content for a key. |
| `GetEmbeddings` | `ItemKey` | `StoredEmbeddingsResponse` | Get every stored vector of a key, in storage order (empty when the key is missing or the entry has no vector). A single-vector entry comes back as a list of one. |
| `SetVector`, `SetVectors`, `AppendVector`, `SetRawVector` | `Vector` | `Result` / `IngestResult` | Store precomputed vectors. `Vectors` is required: a request with an empty list is rejected with `InvalidArgument`, so an old client sending the removed single-vector field fails loudly instead of storing a vector-less entry. |
| `SetDocument` | `Document` | `Result` | Insert/update a document from a raw `Sentence`; the server computes the embedding via its embedding module. |
| `SetVector` | `Vector` | `Result` | Insert/update a document with a precomputed embedding. |
| `SetVectors` | `stream Vector` | `IngestResult` | Bulk insert with precomputed embeddings over a single client-streaming call: one round trip for the whole batch. |
| `SetDocuments` | `stream Document` | `IngestResult` | Bulk insert from raw `Sentence`s; the server batches the embedding calculation (windows of 64 sentences per embedding dispatch). |
| `SearchVector` | `SearchVectorRequest` | `SearchVectorResponse` | Search by a precomputed query vector, optionally with a filter, per-query tuning and a sparse query (hybrid). |
| `SearchDocument` | `SearchDocumentRequest` | `SearchVectorResponse` | Search by a raw `Sentence`; the server computes the embedding and optionally applies a filter, per-query tuning and a sparse query (hybrid). |
| `DeleteVector` | `ItemKey` | `Result` | Delete a document/vector by key. |
| `GetAllKeys` | `CollectionKey` | `KeysResult` | List all keys in a collection (single message — prefer `StreamKeys` on large collections). |
| `StreamKeys` | `StreamKeysRequest` | `stream KeysResult` | Stream all keys in chunks (default 1000 per message); safe at any collection size. |
| `Contains` | `ItemKey` | `Result` | Check whether a key exists. |
| `Clear` | `CollectionKey` | `Result` | Remove all documents from a collection. |
| `Count` | `CollectionKey` | `CountResult` | Count documents in a collection. |
| `CalculateEmbeddings` | `EmbeddingRequest` | `EmbeddingResponse` | Compute an embedding for a sentence, optionally with a task prefix. |
| `CalculateEmbeddingsBatch` | `EmbeddingBatchRequest` | `EmbeddingBatchResponse` | Compute embeddings for many sentences in one call; results keep the input order (blank inputs yield an empty row). |
| `CalculateSparseEmbedding` | `EmbeddingRequest` | `SparseEmbeddingResponse` | Sparse lexical vector of the text: tokenizer term ids and weights, the same text that produces the dense embedding. |
| `CalculateSparseEmbeddingBatch` | `EmbeddingBatchRequest` | `SparseEmbeddingBatchResponse` | One sparse vector per input message, same order. |
| `CalculateImageEmbedding` | `ImageEmbeddingRequest` | `EmbeddingResponse` | Compute the embedding of an image from its raw bytes. Requires `ImagesModelPath` configured (vision model). |
| `CalculateImageEmbeddingBatch` | `ImageEmbeddingBatchRequest` | `EmbeddingBatchResponse` | Compute embeddings for many images in one call; results keep the input order (empty images yield an empty row). |
| `CalculateImageTileEmbeddings` | `ImageEmbeddingRequest` | `ImageTileEmbeddingsResponse` | Compute tile embeddings of an image: overlapping tiles each embedded separately, plus the whole-image embedding as the last entry. |

`IngestResult.Accepted` counts the entries accepted into the server's ingestion pipeline; durability follows the server's group-commit policy, exactly like the unary `Set` operations.

A `SearchVectorRequest` with only `SparseQuery` is a sparse-only search; with both `Embeddings` and `SparseQuery` it is hybrid. `SearchDocumentRequest` can fill the sparse half itself from the sentence (`SparseFromSentence`), so a hybrid search needs nothing but text. `SparseTerms` carries term ids and weights: the server stores them as given and never tokenizes text on its own — the embedding module produces them on request. See [Sparse index](../indexes/sparse.md#producing-the-pairs).

`SetDocument`, `SetDocuments` and `SearchDocument` require an embedding module reachable from the server process (in-process on the all-in-one image, or remote over RabbitMQ from a `jigendb` server) — see [embeddings overview](../embeddings/overview.md). The image RPCs additionally require the vision model to be configured (`JigenEmbeddings:ImagesModelPath`); without it the server runs text-only and image RPCs fail with a clear configuration error.

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

// Weights of the sparse (lexical) terms of one vector: term id to weight.
message SparseTerms {
  map<int32, float> Terms = 1;
}

message Document {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
  bytes Content = 4;
  string Sentence = 5;
  SparseTerms Sparse = 6;   // sparse terms of the document itself
  // Derive the sparse terms from Sentence on the server, with the same tokenizer
  // that produces the dense embedding. Ignored when Sparse is supplied.
  bool SparseFromSentence = 7;
}

// One vector of a multi-vector entry, e.g. one embedding per passage.
message EmbeddingVector {
  repeated float Embeddings = 1;
}

message Vector {
  string Database = 1;
  string Collection = 2;
  bytes Key = 3;
  bytes Content = 4;
  reserved 5; // was a single-vector payload: entries are always multi-vector now
  // Every vector of the entry, all with the collection dimensions. A
  // single-vector entry is a list of one.
  repeated EmbeddingVector Vectors = 6;
  // Sparse terms per vector, aligned by index with Vectors. A shorter list
  // leaves the remaining vectors without sparse terms.
  repeated SparseTerms Sparse = 7;
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
  // Sparse half of the query (term id to weight). When present the search is
  // hybrid and the two halves are fused by reciprocal rank.
  map<int32, float> SparseQuery = 7;
}

message SearchDocumentRequest {
  string Database = 1;
  string Collection = 2;
  string Sentence = 3;
  int32 Top = 4;
  FilterNode Filter = 5;
  SearchTuning Tuning = 6;
  map<int32, float> SparseQuery = 7;
}

// Optional per-query tuning; zero values keep the server defaults.
message SearchTuning {
  int32 EfSearch = 1;                // HNSW beam width override (recall vs latency)
  bool NoContent = 2;                // return keys and scores only
  reserved 3;                        // was MinScore: it duplicated MinDenseScore
  optional float MinDenseScore = 4;  // drop weak dense hits, dense-only or hybrid
  optional float MinSparseScore = 5; // drop weak sparse hits, sparse-only or hybrid
}

message SearchVectorResult {
  bytes Key = 1;
  bytes Content = 2;
  float Score = 3;                     // fused score for a hybrid search, the similarity otherwise
  optional float DenseScore = 4;       // set when the document came from the dense half
  optional float SparseScore = 5;      // set when the document came from the sparse half
}

message SearchVectorResponse {
  repeated SearchVectorResult Results = 1;
}
```

## Collection metrics

`GetCollectionInfo` and `GetCollectionsInfo` report the collection's data sizes
and the metrics of both indexes:

```proto
message CollectionInfoResponse {
  string Name = 1;
  int32 Vectors = 2;
  int32 Dimensions = 3;
  int64 ContentSize = 4;
  int64 VectorSize = 5;
  CollectionIndexInfoResponse Index = 6;    // dense index; absent for exact search
  SparseCollectionInfoResponse Sparse = 7;  // sparse index; absent when the collection has none
}

message SparseCollectionInfoResponse {
  int32 Segments = 1;    // immutable segments read by searches; compaction leaves one
  int32 Terms = 2;       // distinct terms across segments
  int32 Blocks = 3;      // posting blocks across segments
  int32 BlockSize = 4;   // postings per block
  int32 Tombstones = 5;  // retired documents still present until compaction
  int64 SizeBytes = 6;   // on-disk size of the collection's sparse directory
}
```

`Terms`, `Blocks`, `Segments` and `Tombstones` describe the same segments the
searches read. The postings count is not reported: summing it means walking every
posting block, which is not a metrics call.

## Embedding RPCs

Text embedding messages:

```proto
message EmbeddingRequest {
  string Message = 1;
  string Task = 2;
}

// Reply of the embedding service: one vector per input sentence.
message EmbeddingResponse {
  repeated float Embeddings = 1;
}

// Reply of GetEmbeddings: every vector stored for a key, in storage order.
message StoredEmbeddingsResponse {
  repeated EmbeddingVector Vectors = 1;
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

## Error handling

Server-side failures surface to callers as standard gRPC errors (`RpcException` in .NET clients).

The codebase also ships an optional richer mechanism — a server interceptor that attaches an `exception-bin` trailer with a safe payload (original exception type name, message, detail), and a matching client interceptor that rethrows it as a typed exception (`JigenServerException` with `ServerExceptionType` when the original type cannot be reconstructed). **In the current build both interceptors are disabled by default** (commented out in `Jigen.Grpc.Module` and `Jigen.Client.Context`), so expect plain `RpcException`s unless you re-enable them.
