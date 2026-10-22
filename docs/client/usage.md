# Client: usage

This page covers inserting, searching and filtering with `VectorCollection<T>`, once a [`Context` and collection are set up](getting-started.md).

## Inserting

### With a precomputed embedding

```csharp
articles.Add(42, article, embeddings: myFloatVector);

// or, building the entry explicitly:
articles.Add(42, new VectorEntry<Article> { Content = article, Embedding = myFloatVector });
```

Use this form when the embedding is computed client-side (any generator, not necessarily Jigen's) or already available. See [embeddings overview](../embeddings/overview.md) for the "client-side" deployment mode.

### With server-side embedding

```csharp
articles.Add(42, article, sentence: "Jigen is a vector database written in C#.");
```

This calls the gRPC `SetDocument` method, which requires the server to have a reachable embedding module (all-in-one server, or a distributed server with a reachable `jigen-embeddings` worker). See [server overview](../server/overview.md) for the two topologies.

**The sentence fills both slots.** The embedding module generates the dense vector
*and* the sparse lexical vector from the same text, so an entry written this way is
immediately searchable by similarity, by terms and in hybrid, with no second call:

```csharp
await articles.AddAsync(42, article, sentence: "il parco è buio e servono lampioni");

// The server generated both; read the lexical side back:
SparseVector generated = await articles.GetSparseAsync(42);

// A vector you pass explicitly always wins over the generated one:
await articles.AddAsync(42, article,
    embeddings: myDenseVector,          // used as-is
    sparse: SparseVector.Create([7], [1f]));  // used as-is
```

Set `JigenEmbeddings:Sparse:MaxTerms` to bound the generated vector and
`JigenServer:Index:SparseMaxTerms` to bound what the store accepts; keep the first at
or below the second. See [embeddings configuration](../embeddings/configuration.md#sparse-vectors).

### With a sparse (lexical) vector

The sparse slot is a **second, independent vector** on the same entry: term ids
with their weights, supplied by the caller. The server never derives it from the
text, and it is written in the **same call** as the dense vector, so the two
slots always describe the same revision of the entry.

```csharp
using Jigen.Client.BaseTypes;

var entry = new VectorEntry<Article>
{
    Content = article,
    Embedding = denseVector,                                  // optional
    Sparse = SparseVector.Create(
        terms:   [12, 98, 450],
        weights: [0.8f, 1.2f, 0.4f])
};

await articles.AddAsync(42, entry);

// Both vectors in one line, same effect:
await articles.AddAsync(42, article, denseVector, SparseVector.Create([12, 98], [0.8f, 1.2f]));
```

A sparse-only entry is valid — `Embedding` may be null:

```csharp
await articles.AddAsync(42, article, embeddings: null, sparse: SparseVector.Create([7], [1f]));
```

Terms must be **unique, strictly increasing and non-negative**; weights **finite,
non-negative, with at least one above zero**. `FromPairs` sorts the pairs for you
and rejects duplicates; an empty vector is rejected, because `null` is what means
"no sparse slot". The client validates the shape before the call, and the server
validates again against its configured cap (`SparseMaxTerms`, 1024 by default),
so an over-long vector fails with the server's message.

**Writes need the write-ahead log.** A sparse vector cannot be recovered after a
crash without it, so the server refuses such a write when the log is disabled
(`JigenServer:Wal:Enabled`). It is enabled by default.

### Bulk insert

`Add` pays one network round trip per entry. For batches, `AddRangeAsync` streams every entry over a single gRPC call and returns the number of entries the server accepted:

```csharp
// Precomputed embeddings (VectorEntry<T>.Embedding may be null for content-only entries):
int accepted = await articles.AddRangeAsync(entries); // IEnumerable<KeyValuePair<VectorKey, VectorEntry<Article>>>

// Server-side embeddings: the server batches the embedding calculation
// (64 sentences per dispatch) instead of embedding one document at a time.
int accepted = await articles.AddRangeAsync(items);   // IEnumerable<(VectorKey Key, Article Content, string Sentence)>
```

Entries with a null/empty `Sentence` are stored without a vector. Like the unary inserts, acceptance means the entry entered the server's ingestion pipeline; durability follows the server's group-commit policy.

## Async API

Every remote operation has an async counterpart that never blocks a thread on the network call — prefer these in ASP.NET and other async contexts:

```csharp
await articles.AddAsync(42, article, sentence);           // or (key, entry) / (key, content, embeddings)
var entry   = await articles.GetAsync(42);                // null when missing (async TryGetValue)
var results = await articles.SearchAsync(vector, top: 5); // or (sentence[, predicate], top)
var found   = await articles.ContainsKeyAsync(42);
var removed = await articles.RemoveAsync(42);
var count   = await articles.CountAsync();
var keys    = await articles.GetKeysAsync();
await articles.ClearAsync();
```

All of them take an optional `CancellationToken`. The synchronous methods (`Add`, `Search`, the `IDictionary` surface) remain available and unchanged.

## Searching

### By precomputed vector

```csharp
List<VectorSearchResult<Article>> results = articles.Search(myFloatVector, top: 10);
```

### By sentence (server computes the embedding)

```csharp
List<VectorSearchResult<Article>> results = articles.Search("vector databases in .NET", top: 10);
```

### By sentence with a filter predicate

```csharp
var results = articles.Search(
  "vector databases in .NET",
  predicate: a => a.Category == "news",
  top: 10);

var results2 = articles.Search(
  "vector databases in .NET",
  predicate: a => a.Tags.Any(t => t == "ai") && a.Category == "news",
  top: 10);
```

The predicate is a LINQ expression tree; it is translated client-side into the gRPC filter AST (`FilterNode`/`PropertyEqualsCondition`/`PropertyCollectionAnyCondition`/`LogicalCondition`, see [gRPC API](../server/grpc-api.md)) and evaluated server-side against the stored document. Supported shapes are property equality (`x.Prop == value`), collection membership (`x.Tags.Any(t => t == value)`), and `&&`/`||` combinations of those — the same subset documented for [in-process filtering](../in-process/collections.md). The same predicate parameter is available on the vector-based `Search(embeddings, predicate, ...)`.

`Search(sentence, ...)` always requires the server to have an embedding module; `Search(embeddings, top)` never does.

### By example (embedding of an existing document)

`GetEmbedding`/`GetEmbeddingAsync` read back the stored full-precision vector of a key (null when the key is missing or the entry has no vector) — which makes "more like this" a two-liner without an embedding module:

```csharp
var vector = await articles.GetEmbeddingAsync(42);
var similar = await articles.SearchAsync(vector, top: 10);
```

### By sparse (lexical) vector

Exact lexical search: the entries sharing at least one term with the query,
ranked by the sum of the shared weight products. No graph, no approximation.

```csharp
var lexical = await articles.SearchAsync(
    SparseVector.Create(terms: [12, 98], weights: [0.8f, 1.2f]), top: 10);

// With a filter predicate, same subset as the dense search:
var filtered = await articles.SearchAsync(sparseQuery, x => x.Category == "database", top: 10);
```

### Hybrid: fusing the dense and the lexical ranking

Similarity scores and lexical weights live on **different scales**, so summing
them would bury one modality. `SearchHybrid` runs both searches and fuses the
**positions** (reciprocal rank fusion), returning one ordering plus the
provenance of each hit.

```csharp
var hits = await articles.SearchHybridAsync(
    denseVector,
    SparseVector.Create([12, 98], [0.8f, 1.2f]),
    top: 10,
    denseWeight: 1f,
    sparseWeight: 1f);

foreach (var hit in hits)
{
    Console.WriteLine($"{hit.Key} fused={hit.Score:F4} dense={hit.DenseScore} sparse={hit.SparseScore}");
    // InDense / InSparse tell which modality found the entry; a hit found by
    // both precedes one found by a single modality at the same position.
}
```

From a sentence, with both query vectors generated server-side — the shortest
path from text to a fused ranking:

```csharp
var hits = await articles.SearchHybridAsync("mancano lampioni nel parco", top: 10);

// A predicate narrows the candidates, exactly like the other searches:
var filtered = await articles.SearchHybridAsync("mancano lampioni", x => x.Category == "ticket", top: 10);
```

Either vector may be omitted. **When both are supplied explicitly, both weights
must be declared**; a weight of zero excludes that modality from the fusion, and
both at zero is an error. A sentence that generates the second vector needs no
weight: the two modalities are weighed equally.

### Reading the sparse vector back

```csharp
SparseVector sparse = await articles.GetSparseAsync(42);   // null when the entry has none
```

### Per-query tuning

Every `Search`/`SearchAsync` overload accepts an optional `SearchOptions`:

```csharp
var results = await articles.SearchAsync(myFloatVector, top: 10, options: new SearchOptions
{
  EfSearch = 200,    // HNSW beam width for THIS query (recall vs latency); 0 = server default
  NoContent = true,  // keys and scores only: results come back with Content == null
  MinScore = 0.35f   // drop results below this similarity
});
```

`EfSearch` is the knob to recover recall on large collections without changing the server-wide default; it is ignored by exact (brute force) indexes.

### Streaming keys

`Keys`/`GetKeysAsync` return every key in one response, which can exceed the gRPC message limit on large collections. `StreamKeysAsync` streams them in chunks of 1000 (configurable):

```csharp
await foreach (var key in articles.StreamKeysAsync())
  Process(key);
```

## Shared metadata

One metadata record can back several embeddings: the parent passage behind two chunk vectors, or an article indexed by both its title and its body. The record is database-scoped and addressed by an opaque `MetadataIndex`; the association is per collection and key. Reading any associated key returns the shared content, and one update changes what all of them read.

```csharp
// One record for the whole passage.
int passage = articles.CreateMetadata(new Article
{
  Title = "Jigen DB",
  Body = "Jigen is a vector database written in C#."
});

// Two chunk embeddings pointing at it.
articles.Add("chunk-1", firstChunk, firstVector);
articles.Add("chunk-2", secondChunk, secondVector);
articles.AssociateMetadata("chunk-1", passage);
articles.AssociateMetadata("chunk-2", passage);

// Both reads return the passage, not the per-chunk content.
Article a = articles["chunk-1"];
Article b = articles["chunk-2"];
```

| Method | Returns | Notes |
|---|---|---|
| `CreateMetadata(content)` | `int` | Creates the record; it is not associated to anything yet. |
| `UpdateMetadata(index, content)` | `bool` | Replaces the content read by every associated embedding. `false` when the index is unknown. |
| `GetMetadataIndex(key)` | `int` | The associated index, or `-1` when the key is absent or not associated. |
| `AssociateMetadata(key, index)` | `bool` | Associates or moves an existing key. The key must exist and the index must be known. |
| `SearchByMetadata(...)` | `List<MetadataSearchResult<T>>` | Grouped search, see below. |

Every method has an `...Async` counterpart taking a `CancellationToken`.

Deleting a key (`Remove`) or clearing the collection makes the key read as absent; the record itself is not dropped, so it can be associated again.

A content write already creates its own record and associates the key, so `AssociateMetadata` normally *moves* a key from its own record to the shared one. The previous record is left unassociated and stays readable if you kept its index.

### Grouped search

`SearchByMetadata` returns **one result per metadata record**, ranked by the best score observed among its embeddings. It is a separate operation from `Search`: `Search` still returns one result per embedding.

`candidateBudget` is mandatory and has no default, because it is what bounds the work the server may do to complete groups:

```csharp
var groups = await articles.SearchByMetadataAsync(
  queryVector,
  top: 10,
  candidateBudget: 400,
  options: new SearchOptions { EfSearch = 200 });

foreach (var group in groups)
  Console.WriteLine($"{group.MetadataIndex} {group.Score:F3} {group.Content?.Title}");
```

```csharp
// Sentence overload: the server computes the query vector.
var groups = await articles.SearchByMetadataAsync("vector databases in .NET", top: 10, candidateBudget: 400);
```

A predicate restricts the candidates the server collects, exactly like `Search`. It is translated client-side into the filter AST and evaluated server-side:

```csharp
var groups = await articles.SearchByMetadataAsync(
  queryVector,
  predicate: a => a.Category == "news",
  top: 10,
  candidateBudget: 400);
```

The supported predicate shapes are the same subset documented under [Searching](#searching): property equality, collection membership, and `&&`/`||` combinations.

Reading the result:

| Member | Description |
|---|---|
| `MetadataIndex` | Identity of the group. |
| `Key` | The winning embedding of the group. |
| `Content` | The shared content of the record; null with `NoContent`. |
| `Score` | Highest score observed in the group. |

Sizing the budget:

- The budget caps the sum of candidates requested across the internal attempts, repeated candidates included. It is **not** multiplied by `top`.
- When the budget is exhausted, **fewer than `top` groups may come back**. That is a legitimate answer, not an error: raise the budget or `EfSearch` when recall matters more than latency.
- Approximate indexes make **no promise of exact grouped top-k**. Ties are ordered by metadata index.
- Grouped search is single-collection.

Invalid arguments fail before any call: empty embeddings or a blank sentence throw `ArgumentException`, and a non-positive `top` or `candidateBudget` throws `ArgumentOutOfRangeException`.

## Embedding calculation

The `Context` also exposes extension methods to call the server's embedding endpoint directly — useful when you need embeddings for operations outside of `VectorCollection<T>` (e.g. precomputing, caching, or combining with other pipelines):

```csharp
using Jigen.Client;

// single sentence
float[] embedding = context.CalculateEmbeddings("Jigen is a vector database");
float[] embedding2 = await context.CalculateEmbeddingsAsync("Hello world");

// with an optional task description (maps to the model's task prefix)
float[] qaEmbedding = context.CalculateEmbeddings("What is Jigen?", task: "Question");

// batch — processes multiple sentences in one gRPC call
IEnumerable<float[]> batch = context.CalculateEmbeddingsBatch(
  new[] { "sentence one", "sentence two" });

IEnumerable<float[]> asyncBatch = await context.CalculateEmbeddingsBatchAsync(
  new[] { "sentence one", "sentence two" }, task: "Document");
```

The embedding server batches requests internally (up to 64 sentences per model forward pass). Prefer the batch overloads when processing many sentences at once — they reduce round trips and let the server optimise throughput. See [embeddings overview](../embeddings/overview.md) for the supported `task` values and the server-side pipeline.

Image embeddings are available through the same extensions, calling the server's vision model (requires `ImagesModelPath` configured — see [embeddings overview](../embeddings/overview.md#image-embeddings)):

```csharp
// single image (raw bytes; image and text vectors share the embedding space)
float[] imageVector = context.CalculateImageEmbedding(File.ReadAllBytes("photo.jpg"));
float[] imageVector2 = await context.CalculateImageEmbeddingAsync(File.ReadAllBytes("photo.jpg"));

// batch
IEnumerable<float[]> imageBatch = context.CalculateImageEmbeddingsBatch(
  new[] { File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png") });
IEnumerable<float[]> asyncImageBatch = await context.CalculateImageEmbeddingsBatchAsync(
  new[] { File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png") });

// tiles: overlapping tiles of one image, each embedded separately, plus the
// whole-image embedding as the last vector (see embeddings overview)
IEnumerable<float[]> tiles = context.CalculateImageTileEmbeddings(File.ReadAllBytes("photo.jpg"));
IEnumerable<float[]> asyncTiles = await context.CalculateImageTileEmbeddingsAsync(File.ReadAllBytes("photo.jpg"));
```

The vectors returned by the image extensions can be stored and searched together with text embeddings in the same collection — see [embeddings overview](../embeddings/overview.md#image-embeddings) for cross-modal search and tiling details.

## Sharded collections

`ShardedCollection<T>` is a client-side partitioning helper: it creates independent `VectorCollection<T>` instances whose names differ by a shard suffix. The server has no awareness of sharding — each shard is a completely separate collection from the server's perspective.

```csharp
using Jigen.Client;

// create a sharded collection with a base name
var sharded = context.ShardedCollection<Article>("articles");

// each GetShard call returns a normal VectorCollection<T> whose
// collection name is "articles_{shardName}"
var shardA = sharded.GetShard(() => "partition_0");
var shardB = sharded.GetShard(() => "partition_1");

// each shard behaves like any other VectorCollection<T>
shardA.Add(42, article, sentence);
var results = await shardA.SearchAsync(queryVector, top: 10);
```

The shard name is determined lazily by a `Func<string>` delegate, evaluated at each `GetShard` call. This lets you compute the shard name from a key or other runtime context:

```csharp
var sharded = context.ShardedCollection<Article>("articles");
var shard = sharded.GetShard(() => key % 4 == 0 ? "even" : "odd");
```

The shard inherits the parent's `DocumentSerializer`. To query across shards, fan out manually:

```csharp
var partitions = new[] { "partition_0", "partition_1", "partition_2" };
var tasks = partitions.Select(p =>
  sharded.GetShard(() => p).SearchAsync(queryVector, top: 10));
var allResults = (await Task.WhenAll(tasks)).SelectMany(r => r).OrderBy(r => r.Score).Take(10);
```

If you omit the base name, the sharded collection uses `typeof(T).Name`:

```csharp
// equivalent to ShardedCollection<Article>("Article")
var sharded = context.ShardedCollection<Article>();
```

## Dictionary-style access

`VectorCollection<T>` implements `IDictionary<VectorKey, VectorEntry<T>>`:

```csharp
bool exists = articles.ContainsKey(42);
VectorEntry<Article> entry = articles[42];
int count = articles.Count;
ICollection<VectorKey> keys = articles.Keys;
bool removed = articles.Remove(42);
articles.Clear();
```

Each of these maps to one gRPC call (`Contains`, `GetContent`, `Count`, `GetAllKeys`, `DeleteVector`, `Clear`) — there is no local caching, so avoid calling them in a tight loop when a bulk operation is possible instead.

## `VectorKey`

`VectorKey` has implicit conversions from `int`, `uint`, `long`, `ulong`, `Guid`, `string` and `byte[]`, so keys can be passed as plain values everywhere a `VectorKey` is expected:

```csharp
articles.Add(42, article, sentence);         // int
articles.Add(Guid.NewGuid(), article, sentence);
articles.Add("article-42", article, sentence);
```

## Serializer customization

By default, document content is serialized with MessagePack (`MessagePackDocumentSerializer`, contractless). Supply a custom `IDocumentSerializer` through `VectorCollectionOptions<T>` to change this:

```csharp
public class JsonDocumentSerializer : IDocumentSerializer
{
  // implement Serialize/Deserialize using System.Text.Json, etc.
}

var articles = new VectorCollection<Article>(context, new VectorCollectionOptions<Article>
{
  Name = "articles",
  DocumentSerializer = new JsonDocumentSerializer()
});
```

The serializer used by a collection must match whatever the server-side collection expects when deserializing content back (e.g. for `.../documents/{key}/json`, see [REST API](../server/rest-api.md)).

## Error handling

Calls that fail on the server surface as a standard `Grpc.Core.RpcException`:

```csharp
try
{
  articles.Add(42, article, sentence);
}
catch (Grpc.Core.RpcException ex)
{
  Console.WriteLine($"{ex.StatusCode}: {ex.Status.Detail}");
}
```

The library also contains an optional client interceptor that, when enabled together with its server counterpart, turns errors carrying an `exception-bin` trailer into a typed `JigenServerException` (with `ServerExceptionType` set to the original server-side type name). Both interceptors are disabled in the current build — see [gRPC API](../server/grpc-api.md#error-handling) for details.
