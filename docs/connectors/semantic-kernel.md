# Semantic Kernel connector

Jigen ships two `Microsoft.Extensions.VectorData` connectors — the same `VectorStore`/`VectorStoreCollection<TKey,TRecord>` abstractions used by Semantic Kernel's memory/RAG building blocks — one per way of talking to Jigen:

| Package | Wraps | Use when |
|---|---|---|
| `Jigen.SemanticKernel.Store` | `Jigen.Store` (in-process) | Your app embeds the database directly. |
| `Jigen.SemanticKernel.Client` | `Jigen.Client` (gRPC) | Your app talks to a Jigen server. |

Both target the same record model and share the mapping/validation logic (`Jigen.SemanticKernel.Abstractions`, a transitive dependency you don't reference directly).

## Defining a record

Annotate a POCO with the standard `Microsoft.Extensions.VectorData` attributes:

```csharp
using Microsoft.Extensions.VectorData;

public class Article
{
  [VectorStoreKey]
  public Guid Id { get; set; }

  [VectorStoreData]
  public string Title { get; set; } = string.Empty;

  [VectorStoreData]
  public string Category { get; set; } = string.Empty;

  [VectorStoreVector(1536)]
  public ReadOnlyMemory<float> Embedding { get; set; }
}
```

Two rules the connectors enforce at collection-construction time (not on first use — a misconfigured record fails immediately and loudly):

- **Exactly one `[VectorStoreVector]` property.** Jigen stores a single embedding per entry; a record with zero or several throws an `ArgumentException`.
- **`TKey` is `Guid` or `string`.** Any other key type throws `NotSupportedException` — this mirrors what Jigen's own `VectorKey` converts from directly.

The vector property can be `float[]`, `ReadOnlyMemory<float>`, or `Embedding<float>` (from `Microsoft.Extensions.AI`).

Records must have a public parameterless constructor — the same constraint `Jigen.VectorCollection<T>`/`Jigen.Client.VectorCollection<T>` already have.

## In-process: `Jigen.SemanticKernel.Store`

```bash
dotnet add package Jigen.SemanticKernel.Store
```

```csharp
using Jigen;
using Jigen.SemanticKernel.Store;

var store = new Store(new StoreOptions { DataBasePath = "/data/jigendb", DataBaseName = "demo" });
var vectorStore = new JigenStoreVectorStore(store);

var articles = vectorStore.GetCollection<Guid, Article>("articles");
await articles.EnsureCollectionExistsAsync(); // no-op: Jigen creates collections on first write

await articles.UpsertAsync(new Article
{
  Id = Guid.NewGuid(),
  Title = "Hello, Jigen!",
  Category = "news",
  Embedding = embeddingVector // computed upstream, see "Embeddings" below
});

await foreach (var hit in articles.SearchAsync(queryVector, top: 5))
  Console.WriteLine($"{hit.Record.Title} (score {hit.Score})");

await store.SaveChangesAsync();
await store.Close();
```

`JigenStoreVectorStore` does not own the `Store`'s lifetime — close it yourself, same as any other in-process usage (see [in-process getting started](../in-process/getting-started.md)).

## Client: `Jigen.SemanticKernel.Client`

```bash
dotnet add package Jigen.SemanticKernel.Client
```

The database itself must already exist on the server — unlike collections (created implicitly on first write), Jigen's server does not create databases on demand. Create it once, e.g. `POST /api/database?name=demo` (see [REST API](../server/rest-api.md)), before pointing a `Context` at it.

```csharp
using Jigen.Client;
using Jigen.SemanticKernel.Client;

var context = new Context(new ConnectionOptions
{
  HostName = "localhost",
  Port = 3223,
  DatabaseName = "demo"
});

var vectorStore = new JigenClientVectorStore(context);
var articles = vectorStore.GetCollection<Guid, Article>("articles");

await articles.UpsertAsync(new Article
{
  Id = Guid.NewGuid(),
  Title = "Hello, Jigen!",
  Embedding = embeddingVector
});

var hit = await articles.GetAsync(articleId, new RecordRetrievalOptions { IncludeVectors = true });
```

See [client getting started](../client/getting-started.md) for `ConnectionOptions` and the recommended "subclass `Context`" pattern — it composes fine with the connector, just pass your subclass instance to `JigenClientVectorStore`.

## Searching and filtering

`SearchAsync` takes a pre-computed vector (`float[]`, `ReadOnlyMemory<float>`, or `Embedding<float>`) and an optional `VectorSearchOptions<TRecord>.Filter`, passed straight through to Jigen's own `Expression<Func<TRecord,bool>>`-based filtering — no separate translation layer:

```csharp
var results = articles.SearchAsync(queryVector, top: 5, new VectorSearchOptions<Article>
{
  Filter = a => a.Category == "news",
  IncludeVectors = true,
  ScoreThreshold = 0.75
});
```

`GetAsync(Expression<Func<TRecord,bool>> filter, int top, ...)` (a predicate scan with no vector) is also implemented, but reads every key and evaluates the predicate client-side — Jigen has no filter-only scan primitive independent of a vector query. Fine for small/medium collections; don't rely on it for large ones.

## Embeddings are out of scope

Neither connector wires up an `IEmbeddingGenerator<string, Embedding<float>>`: records must arrive with `Embedding` already populated, and `SearchAsync` only accepts a pre-computed vector as the search value (anything else throws `NotSupportedException`). Generate embeddings upstream — e.g. with `Jigen.SemanticTools`'s `SentenceEmbedder` (see [embeddings overview](../embeddings/overview.md)) or any other `IEmbeddingGenerator` — before calling `UpsertAsync`/`SearchAsync`.

## Known limitations (v1)

- **Dynamic collections** (`VectorStore.GetDynamicCollection`, schema-less `Dictionary<string,object?>` records) are not supported — throws `NotSupportedException`. Use a typed record.
- **`EnsureCollectionDeletedAsync`** is best-effort: Jigen has no separate collection-metadata record to drop, so it clears every entry instead. The collection then also stops appearing in `ListCollectionNamesAsync`/`CollectionExistsAsync` (clearing removes its key from the store's position index), matching the semantics of a real delete for practical purposes.
- **No per-query HNSW tuning** (`efSearch`) — `Microsoft.Extensions.VectorData.VectorSearchOptions<TRecord>` doesn't expose it in this SDK version, so both connectors search with the index's configured default.
- **`Skip`** is emulated by over-fetching `top + Skip` results and skipping locally — there is no server/index-side skip.
