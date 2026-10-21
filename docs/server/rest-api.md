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
| `PUT` | `/api/database/{name}/users` | Replace the set of users with access to the database (body: `{ "Users": [{ "UserId": "...", "UserName": "..." }] }`). |

`GET /api/database/{name}/details` returns a `DatabaseDetails` object: `Name`, `CreatedAtUtc`, `Vectors`, `ContentSize`, `VectorSize`, `AllocatedContentSize`, `AllocatedVectorSize`, `ContentFreeSpace`, `VectorFreeSpace`, `CollectionsCount`, `UsersCount`, and per-collection `Collections` (`Name`, `Vectors`, `Dimensions`, `ContentSize`, `VectorSize`).

User/database access management is part of the identity module; it is only mentioned briefly here, since the web admin UI it primarily serves is out of scope for this documentation.

## Collections

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/database/{db}/collections` | List collection names in a database. |
| `GET` | `/api/database/{db}/collections/{collection}/info` | Collection info: `Name`, `Vectors`, `Dimensions`, `ContentSize`, `VectorSize`. |
| `POST` | `/api/database/{db}/collections/search` | Search across one or more collections. Body: `SearchCollectionsData`. |
| `POST` | `/api/database/{db}/collections/{collection}/documents/{key}` | Create/insert a document. Body: `DocumentPayload`. |
| `PUT` | `/api/database/{db}/collections/{collection}/documents/{key}` | Same as `POST` (upsert). |
| `PATCH` | `/api/database/{db}/collections/{collection}/documents/{key}` | Same as `POST`/`PUT`. |
| `GET` | `/api/database/{db}/collections/{collection}/documents/{key}` | Get the raw serialized content for a key. |
| `GET` | `/api/database/{db}/collections/{collection}/documents/{key}/json` | Get the content deserialized to JSON. |
| `DELETE` | `/api/database/{db}/collections/{collection}/documents/{key}` | Delete a document/vector by key. |
| `GET` | `/api/database/{db}/collections/{collection}/vectors/{key}/sparse` | Read the sparse vector of a key as `{"terms":[...],"weights":[...]}`; empty arrays when the entry has none. |
| `POST` | `/api/database/{db}/collections/{collection}/search/sparse` | Exact lexical search. Body: `SparseSearchData`. |
| `POST` | `/api/database/{db}/collections/{collection}/search/sparse-by-metadata` | Grouped lexical search, one result per shared metadata record. Body: `SparseByMetadataData`; `candidateBudget` is mandatory. |
| `POST` | `/api/database/{db}/collections/{collection}/search/hybrid` | Dense search, lexical search and their fusion. Body: `HybridSearchData`. |

`{key}` is converted to the binary `VectorKey` layout the document was stored with, which depends on the CLR type used at insert time (`int` = 4 bytes, `long` = 8, `guid` = 16, `string` = UTF-8). By default the type is detected from the value (GUID-shaped → `guid`, integer → `long`, anything else → `string`); pass `?keyType=string|int|long|guid` to force a specific layout — e.g. `?keyType=int` to read a document inserted with a 4-byte `int` key, or `?keyType=string` for a key like `"42"` that would otherwise be detected as a number.

### `SearchCollectionsData` (search request body)

| Field | Type | Description |
|---|---|---|
| `Collections` | string[] | One or more collection names to search (required). |
| `Sentence` | string | Free-text query; the server computes the embedding (requires an embedding module — all-in-one, or a `jigendb` server with `Kaido:Enabled` and a reachable embedding worker). Mutually exclusive with `Embeddings` but one of the two is required. |
| `Embeddings` | float[] | A precomputed query vector. Use this to search without depending on the server's embedding module. |
| `Top` | int | Number of results to return. |

The response (`SearchCollectionsResult`) reports timing breakdown (`EmbeddingsCalculationTime`, `SearchTime`, `MergeTime`, `SortingTime`) plus per-collection results (`CollectionsResults`, each with its own `Results`) and a single `MergedResults` list ranked across all requested collections. Each result item carries `Key`, `Content`, `Score`.

Note: the server's Newtonsoft.Json configuration does not add a camelCase contract resolver, so both request and response JSON use the C# (PascalCase) property names shown above; request deserialization is case-insensitive, but responses will come back PascalCase.

### `DocumentPayload` (insert/update request body)

| Field | Type | Description |
|---|---|---|
| `Payload` | object | The document content, serialized server-side with the collection's configured `IDocumentSerializer` (MessagePack by default). |
| `Sentence` | string | Text to embed and store as the document's vector. Requires an embedding module, same as search by sentence. |
| `Sparse` | `SparseVector` | Optional lexical slot, independent of `Sentence`: the server never derives it from the text. |

### Sparse (lexical) vectors

A sparse vector is a **second, independent vector** on the same entry: term ids
with their weights. The same object appears in `VectorPayload`, `DocumentPayload`,
`BulkVectorItem` and `BulkDocumentItem`, so a document can be written with a
precomputed embedding, a sentence, a sparse vector, or any combination.

```json
{
  "payload": { "title": "Il documento" },
  "embeddings": [0.12, -0.34, 0.56],
  "sparse": { "terms": [12, 98, 450], "weights": [0.8, 1.2, 0.4] }
}
```

`POST` / `PUT /vectors/{key}` accepts **either vector**: an entry with only
`sparse` is valid, and so is one with only `embeddings`.

| Field | Type | Description |
|---|---|---|
| `terms` | int[] | Caller-defined term ids: unique, strictly increasing, non-negative. |
| `weights` | float[] | One per term, in the same order: finite, non-negative, at least one above zero. |

A malformed vector is refused with `400` and a message naming the offending term
or the configured cap (`JigenServer:Index:SparseMaxTerms`, 1024 by default).
An **empty** vector is refused as well: omit `sparse` to store no lexical slot.

**Writes need the write-ahead log** (`JigenServer:Wal:Enabled`, on by default).
Without it the server refuses a write carrying terms with `400`, because the
slot could not be recovered after a crash.

### `SparseSearchData` (lexical search body)

| Field | Type | Description |
|---|---|---|
| `Sparse` | `SparseVector` | The query terms and weights (required). |
| `Top` | int | Number of results to return. |
| `NoContent` | bool | Return keys and scores only. |
| `MinScore` | float? | Drop results scoring below this value. |

### `SparseByMetadataData` (grouped lexical search body)

Same as above plus **`CandidateBudget`** (required, greater than zero): it caps
the entries requested from the term lists, so a group-rich collection cannot turn
one query into a full scan.

### `HybridSearchData` (fusion body)

| Field | Type | Description |
|---|---|---|
| `Sentence` | string | Dense query embedded server-side. |
| `Embeddings` | float[] | Dense query vector, already computed. |
| `Sparse` | `SparseVector` | Lexical query. |
| `Top` | int | Number of fused results. |
| `DenseWeight` | float? | Weight of the dense ranking in the fusion. |
| `SparseWeight` | float? | Weight of the lexical ranking in the fusion. |
| `NoContent` | bool | Return keys and scores only. |

At least one of `sentence`, `embeddings` or `sparse` is required. **When both
vectors are supplied, both weights must be declared**; a weight of zero excludes
that modality, and both at zero is refused. Fusion is by **position**
(reciprocal rank fusion), because a similarity score and a lexical weight are not
comparable: `Score` is the fused score, while `DenseScore` and `SparseScore` keep
the original ones and `InDense`/`InSparse` report which modality found the entry.

## Shared metadata

A metadata record is content shared by several embeddings — for example the parent passage behind two chunk vectors, or an article indexed by both its title and its body. The content is global to the **database**; the association is per **collection and embedding key**. Many embeddings may reference the same record, and updating the record once changes what every associated embedding reads.

The record is addressed by `MetadataIndex`, an opaque `int32` assigned by the server. It is stable across reopen and compaction and never reused after retirement. The value `-1` means "no association" and is never a valid index.

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/database/{db}/metadata` | Create a metadata record. Body: `MetadataPayload`. Returns `201` with `{ "metadataIndex": n }`. |
| `PUT` | `/api/database/{db}/metadata/{metadataIndex}` | Replace the content of a record. Every associated embedding reads the new content. |
| `GET` | `/api/database/{db}/collections/{collection}/documents/{key}/metadata` | Read the metadata index associated to an embedding key. |
| `PUT` | `/api/database/{db}/collections/{collection}/documents/{key}/metadata` | Associate or reassociate an existing embedding key. Body: `MetadataAssociationPayload`. |
| `POST` | `/api/database/{db}/collections/{collection}/search/metadata` | Grouped search: one result per metadata record. Body: `MetadataSearchData`. |

`MetadataPayload` is the same shape used for document inserts — `{ "Payload": {...} }` — and is serialized with the collection's document serializer, so the record content and the document content remain interchangeable.

`MetadataAssociationPayload` carries `MetadataIndex`. The key is resolved like every other document route: pass `?keyType=string|int|long|guid` when the automatic detection would pick the wrong layout.

### Behaviour

- Reading a document (`GET .../documents/{key}`) of an associated key returns the **shared** metadata content, not a per-vector copy. Two embeddings associated to the same record therefore read identical content.
- `SetVector`/`SetDocument` and the bulk equivalents on an already associated key update the shared record, and the change is visible to every associated embedding.
- `DELETE .../documents/{key}` and `Clear` make the key read as absent. The record itself is not dropped — not even by compaction — so it can be associated again later.
- A write is visible when the call returns. Durability follows the store's own policy (subsequent save), as for every other write.
- `PUT /api/database/{db}/metadata/{metadataIndex}` answers `200` on success and `404` with `{ "updated": false }` when the index is unknown.
- `GET .../documents/{key}/metadata` answers `404` when the key has no association, including when the key does not exist.
- `PUT .../documents/{key}/metadata` answers `400` when the key is not valid for the requested key type, the embedding key does not exist, or the metadata index does not exist.

### `MetadataSearchData` (grouped search body)

| Field | Type | Description |
|---|---|---|
| `Sentence` | string | Query text; the server computes the vector. One of `Sentence`/`Embeddings` is required. |
| `Embeddings` | float[] | Precomputed query vector; avoids depending on the server's embedding module. |
| `Top` | int | Requested number of groups. Defaults to `10`; must be greater than zero. |
| `CandidateBudget` | int | **Mandatory.** Caps the sum of candidates requested across the internal attempts, repeated candidates included. Must be greater than zero. |
| `EfSearch` | int | HNSW beam width for this query; `0` keeps the server default. |
| `NoContent` | bool | Return identity and score only; each result's `Content` is null. |
| `MinScore` | float? | Drop groups whose winning score is below this similarity. |

The response is an array of result items: `MetadataIndex`, `Key` (base64 of the winning embedding key), `Content` (base64 of the serialized content, null with `NoContent`), `Score` (highest score observed in the group). Ties are ordered by metadata index.

Two properties of the contract matter when sizing a query:

- `CandidateBudget` is bound per query, so grouped search cannot silently widen to the whole collection. When the budget is exhausted, **fewer than `Top` groups may come back**.
- Grouped search is ranked by the best observed embedding score, not by a global multi-collection ranking, and it makes **no promise of exact top-k** on approximate indexes. Raise `EfSearch` or the budget to improve recall.

Grouped search here is single-collection. Filtering is available on the gRPC channel only (see [gRPC API](grpc-api.md#shared-metadata)); the REST channel has no approved predicate translation for it.

### curl example

Create a record, associate two embeddings to it, then read one back:

```bash
# 1. Create the shared content — the response carries the opaque index.
curl -X POST http://localhost:13223/api/database/mydb/metadata \
  -H "Content-Type: application/json" \
  -d '{ "Payload": { "title": "Jigen DB", "body": "A vector database written in C#." } }'
# → 201 {"metadataIndex":12}

# 2. Associate two different embeddings to the same record.
#    Both keys must already exist in the collection.
curl -X PUT "http://localhost:13223/api/database/mydb/collections/chunks/documents/chunk-1/metadata" \
  -H "Content-Type: application/json" -d '{ "MetadataIndex": 12 }'
curl -X PUT "http://localhost:13223/api/database/mydb/collections/chunks/documents/chunk-2/metadata" \
  -H "Content-Type: application/json" -d '{ "MetadataIndex": 12 }'

# 3. Read the association, then update the content once for both.
curl "http://localhost:13223/api/database/mydb/collections/chunks/documents/chunk-1/metadata"
# → 200 {"metadataIndex":12}

curl -X PUT http://localhost:13223/api/database/mydb/metadata/12 \
  -H "Content-Type: application/json" \
  -d '{ "Payload": { "title": "Jigen DB", "body": "Updated once, read by every associated embedding." } }'

# 4. Grouped search: the budget is mandatory.
curl -X POST http://localhost:13223/api/database/mydb/collections/chunks/search/metadata \
  -H "Content-Type: application/json" \
  -d '{
        "Sentence": "vector databases in .NET",
        "Top": 10,
        "CandidateBudget": 400
      }'
```

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
