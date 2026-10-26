# REST API

The REST API is served on port `13223` (see [server overview](overview.md)). Base URL: `http://host:13223`.

An interactive reference (Scalar UI) is available at `/scalar`, backed by the OpenAPI document at `/openapi/v1.json`.

All request/response bodies are JSON.

## Databases

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/database?name={name}` | Create a database. |
| `DELETE` | `/api/database?name={name}` | Delete a database. |
| `GET` | `/api/database` | List database names. |
| `GET` | `/api/database/{name}/details` | Database details: size, vector/collection counts, users. |
| `GET` | `/api/database/{name}/users` | List users with access to the database. |
| `POST` | `/api/database/{name}/shrink` | Compact the database: data files, vector index and sparse segments. Ingestion pauses while it runs; `409` when a shrink is already running. |
| `PUT` | `/api/database/{name}/users` | Replace the set of users with access to the database (body: `{ "Users": [{ "UserId": "...", "UserName": "..." }] }`). |

`GET /api/database/{name}/details` returns a `DatabaseDetails` object: `Name`, `CreatedAtUtc`, `Vectors`, `ContentSize`, `VectorSize`, `IndexSize` (dense graphs), `SparseSize` (sparse indexes), `AllocatedContentSize`, `AllocatedVectorSize`, `ContentFreeSpace`, `VectorFreeSpace`, `CollectionsCount`, `UsersCount`, and per-collection `Collections` (`Name`, `Vectors`, `Dimensions`, `ContentSize`, `VectorSize`, `Index`, `Sparse`).

The sparse block reports `Segments`, `Terms`, `Blocks`, `BlockSize`, `Tombstones` and `SizeBytes`; `Tombstones` counts the documents retired by a delete or an overwrite that are still on disk until the next `Shrink`, which compacts the segments and clears them.

User/database access management is part of the identity module; it is only mentioned briefly here, since the web admin UI it primarily serves is out of scope for this documentation.

## Collections

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/database/{db}/collections` | List collection names in a database. |
| `GET` | `/api/database/{db}/collections/{collection}/info` | Collection info: `Name`, `Vectors`, `Dimensions`, `ContentSize`, `VectorSize`, `Index` (dense index metrics, absent for exact search) and `Sparse` (sparse index metrics, absent when the collection has none). |
| `POST` | `/api/database/{db}/collections/search` | Search across one or more collections. Body: `SearchCollectionsData`. |
| `POST` | `/api/database/{db}/collections/{collection}/search` | Search one collection by sentence, by vector, by sparse weights, or both (hybrid). Body: `SearchData`. |
| `POST` | `/api/database/{db}/collections/{collection}/vectors/{key}` | Insert a vector entry. Body: `VectorPayload`. |
| `POST` | `/api/database/{db}/collections/{collection}/vectors/bulk` | Insert many vector entries in one request. Body: `BulkVectorItem[]`. |
| `POST` | `/api/database/{db}/collections/{collection}/documents/{key}` | Create/insert a document. Body: `DocumentPayload`. |
| `PUT` | `/api/database/{db}/collections/{collection}/documents/{key}` | Same as `POST` (upsert). |
| `PATCH` | `/api/database/{db}/collections/{collection}/documents/{key}` | Same as `POST`/`PUT`. |
| `GET` | `/api/database/{db}/collections/{collection}/documents/{key}` | Get the raw serialized content for a key. |
| `GET` | `/api/database/{db}/collections/{collection}/documents/{key}/json` | Get the content deserialized to JSON. |
| `DELETE` | `/api/database/{db}/collections/{collection}/documents/{key}` | Delete a document/vector by key. |

`{key}` is converted to the binary `VectorKey` layout the document was stored with, which depends on the CLR type used at insert time (`int` = 4 bytes, `long` = 8, `guid` = 16, `string` = UTF-8). By default the type is detected from the value (GUID-shaped → `guid`, integer → `long`, anything else → `string`); pass `?keyType=string|int|long|guid` to force a specific layout — e.g. `?keyType=int` to read a document inserted with a 4-byte `int` key, or `?keyType=string` for a key like `"42"` that would otherwise be detected as a number.

### `SearchCollectionsData` (search request body)

| Field | Type | Description |
|---|---|---|
| `Collections` | string[] | One or more collection names to search (required). |
| `Sentence` | string | Free-text query; the server computes the embedding (requires an embedding module — all-in-one, or a `jigendb` server with `Kaido:Enabled` and a reachable embedding worker). Mutually exclusive with `Embeddings` but one of the two is required. |
| `Embeddings` | float[] | A precomputed query vector. Use this to search without depending on the server's embedding module. |
| `Top` | int | Number of results to return. |
| `SparseFromSentence` | bool | Derive the sparse half from `Sentence`, turning the query into a hybrid search fused by reciprocal rank. Defaults to `true`; send `false` for a dense-only search. |
| `MinDenseScore` | float? | Drop dense hits below this similarity. |
| `MinSparseScore` | float? | Drop sparse hits below this score. |

The response (`SearchCollectionsResult`) reports timing breakdown (`EmbeddingsCalculationTime`, `SearchTime`, `MergeTime`, `SortingTime`) plus per-collection results (`CollectionsResults`, each with its own `Results`) and a single `MergedResults` list ranked across all requested collections. Each result item carries `Key`, `Content`, `Score` (the fused RRF score for a hybrid search) and, when the server computed them, `DenseScore` and `SparseScore`.

Note: the server's Newtonsoft.Json configuration does not add a camelCase contract resolver, so both request and response JSON use the C# (PascalCase) property names shown above; request deserialization is case-insensitive, but responses will come back PascalCase.

### `SearchData` (single-collection search body)

| Field | Type | Description |
|---|---|---|
| `Sentence` | string | Free-text query; the server computes the embedding (requires an embedding module). |
| `Embeddings` | float[] | A precomputed query vector. |
| `SparseQuery` | object | Sparse half of the query: term id (as a JSON key) to weight, e.g. `{"1204": 1.0, "77": 0.4}`. Alone it is a sparse-only search; together with `Sentence` or `Embeddings` it is hybrid. |
| `SparseFromSentence` | bool | Derive the sparse half from `Sentence` with the same tokenizer that produces the dense embedding: a hybrid search from a sentence alone. Ignored when `SparseQuery` is present. Defaults to `true`; send `false` for a dense-only search. |
| `Top` | int | Number of results to return (default 10). |
| `MinDenseScore` | float? | Drop dense hits below this similarity: on a dense-only search and on the dense half of a hybrid one. |
| `MinSparseScore` | float? | Drop sparse hits below this score: on a sparse-only search and on the sparse half of a hybrid one. |

One of `Sentence`, `Embeddings` or `SparseQuery` is required. Each result carries `Key`, `Content`, `Score` (the fused score for a hybrid search) and, when the server computed them, `DenseScore` and `SparseScore`. The term ids in `SparseQuery` are your vocabulary — the server does not tokenize text; see [Sparse index](../indexes/sparse.md).

### `VectorPayload` (vector insert body)

| Field | Type | Description |
|---|---|---|
| `Payload` | object | The document content, serialized server-side with the collection's `IDocumentSerializer`. |
| `Vectors` | float[][] | Every vector of the entry; a single-vector entry is an array of one. |
| `Sparse` | object[] | Sparse terms per vector, aligned by index with `Vectors`: one object mapping term id to weight per vector, e.g. `[{"1204": 1.0}]`. A longer list than `Vectors`, or a negative term id, is rejected with 400. |

### `DocumentPayload` (insert/update request body)

| Field | Type | Description |
|---|---|---|
| `Payload` | object | The document content, serialized server-side with the collection's configured `IDocumentSerializer` (MessagePack by default). |
| `Sentence` | string | Text to embed and store as the document's vector. Requires an embedding module, same as search by sentence. |
| `Sparse` | object | Sparse terms of the document: term id to weight, e.g. `{"1204": 1.0, "77": 0.4}`. Optional; negative term ids are rejected with 400. |
| `SparseFromSentence` | bool | Derive the sparse terms from `Sentence` on the server, with the same tokenizer that produces the dense embedding, so a write fills both slots. Ignored when `Sparse` is present. Defaults to `true`; send `false` to store the document dense-only. |

## Embeddings

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/embeddings/tasks` | List configured task prefixes (`JigenEmbeddings:Tasks`). |
| `POST` | `/api/embeddings/calculate` | Compute an embedding for a JSON string body, using the default task. |
| `POST` | `/api/embeddings/calculate/{task}` | Compute an embedding for a JSON string body, using the given task prefix. |
| `POST` | `/api/embeddings/calculate-image` | Compute an image embedding. Body: base64 string (JSON) or a `file` field (`multipart/form-data`). Requires `ImagesModelPath` configured. |
| `POST` | `/api/embeddings/calculate-image/batch` | Compute image embeddings for many images. Body: array of base64 strings (JSON) or a `files` field (`multipart/form-data`). |
| `POST` | `/api/embeddings/calculate-image/tiles` | Compute tile embeddings: overlapping tiles of the image, each embedded separately, plus the whole-image embedding as the last vector. Body: base64 string (JSON) or a `file` field (`multipart/form-data`). Tiling parameters come from `ImageGeneratorOptions`. |

These endpoints require an embedding module to be reachable (all-in-one server, or a `jigendb` server routing to `jigen-embeddings` over RabbitMQ). The image endpoints additionally require `JigenEmbeddings:ImagesModelPath` to be set — without it the server runs text-only and image requests fail with a clear configuration error. See [embeddings overview](../embeddings/overview.md).

## curl examples

Search a collection by sentence:

```bash
curl -X POST http://localhost:13223/api/database/mydb/collections/search \
  -H "Content-Type: application/json" \
  -d '{
        "Collections": ["articles"],
        "Sentence": "vector databases in .NET",
        "Top": 5
      }'
```

Insert a document with server-side embedding:

```bash
curl -X POST "http://localhost:13223/api/database/mydb/collections/articles/documents/42" \
  -H "Content-Type: application/json" \
  -d '{
        "Payload": { "title": "Jigen DB", "category": "news" },
        "Sentence": "Jigen is a vector database written in C#."
      }'
```

Compute an embedding directly:

```bash
curl -X POST http://localhost:13223/api/embeddings/calculate/search_document \
  -H "Content-Type: application/json" \
  -d '"Jigen is a vector database written in C#."'
```

Compute an image embedding (JSON base64):

```bash
curl -X POST http://localhost:13223/api/embeddings/calculate-image \
  -H "Content-Type: application/json" \
  -d '"/9j/4AAQSkZJRgABAQEA..."'
```

Compute image embeddings from a file (multipart):

```bash
curl -X POST http://localhost:13223/api/embeddings/calculate-image \
  -F "file=@/path/to/photo.jpg"
```

Compute tile embeddings from a file:

```bash
curl -X POST http://localhost:13223/api/embeddings/calculate-image/tiles \
  -F "file=@/path/to/photo.jpg"
```

For the equivalent gRPC operations, see [gRPC API](grpc-api.md).
