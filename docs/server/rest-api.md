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

`{key}` accepts a bare string, or is matched as `int`/`guid`/`long` route constraints — the controller converts it to a `VectorKey` accordingly.

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

## Embeddings

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/embeddings/tasks` | List configured task prefixes (`JigenEmbeddings:Tasks`). |
| `POST` | `/api/embeddings/calculate` | Compute an embedding for a JSON string body, using the default task. |
| `POST` | `/api/embeddings/calculate/{task}` | Compute an embedding for a JSON string body, using the given task prefix. |

These endpoints require an embedding module to be reachable (all-in-one server, or a `jigendb` server routing to `jigen-embeddings` over RabbitMQ). See [embeddings overview](../embeddings/overview.md).

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

For the equivalent gRPC operations, see [gRPC API](grpc-api.md).
