# Client: usage

This page covers inserting, searching and filtering with `VectorCollection<T>`, once a [`Context` and collection are set up](getting-started.md).

## Inserting

### With a precomputed embedding

```csharp
articles.Add(42, article, embeddings: myFloatVector);

// or, building the entry explicitly:
articles.Add(42, new VectorEntry<Article> { Content = article, Embeddings = [myFloatVector] });
```

Use this form when the embedding is computed client-side (any generator, not necessarily Jigen's) or already available. See [embeddings overview](../embeddings/overview.md) for the "client-side" deployment mode.

### With several vectors

An entry can store more than one vector — for example one per passage of a long document. All vectors of an entry must share the collection dimensions.

```csharp
var articles = context.Collection<Article>("articles");

// One vector.
articles.Add(42, new Article { Title = "Single vector" }, new[] { 1f, 0f, 0f });

// Several vectors: same overload, one entry per vector.
articles.Add(43, new Article { Title = "Multi vector" }, new float[][]
{
  [1f, 0f, 0f],   // passage 1
  [0f, 1f, 0f],   // passage 2
  [0f, 0f, 1f]    // passage 3
});

// Or build the entry explicitly.
articles.Add(44, new VectorEntry<Article>
{
  Content = new Article { Title = "Multi vector" },
  Embeddings = [[1f, 0f, 0f], [0f, 1f, 0f]]
});

float[][] all = articles.GetEmbeddings(43);   // [[1,0,0], [0,1,0], [0,0,1]]

var hits = articles.SearchDense(new[] { 0f, 1f, 0f }, top: 2); // finds 43 through its second vector
```

An entry is always a list of vectors: one stored vector comes back as a list of one, and there is no separate single-vector reader. Search scores a document by its closest vector — exact collections scan all of them, HNSW collections index one graph node per vector and merge the nodes of a document by best score.

### With server-side embedding

```csharp
articles.Add(42, article, sentence: "Jigen is a vector database written in C#.");
```

This calls the gRPC `SetDocument` method, which requires the server to have a reachable embedding module (all-in-one server, or a distributed server with a reachable `jigen-embeddings` worker). See [server overview](../server/overview.md) for the two topologies.

### With sparse (lexical) terms

A document written with precomputed vectors can also carry sparse `(term id, weight)` pairs, which feed the collection's sparse index and the hybrid search. A one-vector entry takes one mapping; a multi-vector entry takes one mapping per vector, aligned by index.

```csharp
// One vector, one sparse list.
articles.Add(42, new Article { Title = "Sparse" }, new[] { 0.1f, 0.2f },
  new Dictionary<int, float> { [1204] = 1.0f, [77] = 0.4f });

// Several vectors, one sparse list each.
articles.Add(43, new Article { Title = "Multi" },
  new float[][] { [0.1f, 0.2f], [0.3f, 0.4f] },
  new List<IReadOnlyDictionary<int, float>>
  {
    new Dictionary<int, float> { [1204] = 1.0f },
    new Dictionary<int, float> { [77] = 0.4f }
  });

// Sentence-based: the server derives the sparse terms from the text.
articles.Add(44, new Article { Title = "By text" }, sentence: "sparse retrieval");
```

Term ids and weights come from your own vocabulary — the collection stores them as given, and a query must use the same ids. A sparse list longer than the vector list, or a single mapping on a multi-vector entry, is refused client-side with an `ArgumentException`. See [Sparse index](../indexes/sparse.md) for what the index does with them.

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

The three entry points follow the in-process names: `SearchDense`/`SearchDenseAsync` for the **dense** half, `SearchSparse`/`SearchSparseAsync` for the **sparse** half, and `Search`/`SearchAsync` for the **hybrid** search that fuses the two. `SearchHybrid` from earlier previews is gone: it is `Search` now.

A method that takes a sentence never takes vectors or sparse terms: the server computes the dense embedding and derives the sparse half from the same text, so the sentence forms of `Search` are always hybrid. Dense-only search takes a precomputed vector.

### Dense, by precomputed vector

```csharp
List<VectorSearchResult<Article>> results = articles.SearchDense(myFloatVector, top: 10);
```

### Sparse only

```csharp
var results = articles.SearchSparse(
  new Dictionary<int, float> { [1204] = 1.0f, [77] = 0.4f },
  top: 10);
```

Each result carries the sparse score in `SparseScore` and no `DenseScore`. The `top` parameter is the number of documents to return.

### Hybrid (dense + sparse)

Hybrid results are fused by reciprocal rank: a document scores `1 / (60 + position)` in each half where it appears and the contributions are summed, so `Score` is the fused value while `DenseScore`/`SparseScore` keep the partial ones. Either half may be omitted, but not both.

```csharp
var hybrid = articles.Search(myFloatVector, new Dictionary<int, float> { [1204] = 1.0f }, top: 10);

// With a predicate and per-half cuts applied before the fusion.
var tuned = articles.Search(myFloatVector, sparseQuery, predicate: a => a.Category == "news", top: 10,
  options: new SearchOptions { MinDenseScore = 0.30f, MinSparseScore = 0.05f });

// Sparse half alone is the sparse search; the dense half alone is SearchDense.
```

A document that is weak in one half cannot be lifted into the result by the other: `MinDenseScore` and `MinSparseScore` drop it before the fusion, and the sparse cut also lets that half prune earlier.

`MinDenseScore` and `MinSparseScore` are the only score floors, and each one means the same thing on every path: `MinDenseScore` cuts the dense half of a `SearchDense` as well as of a hybrid `Search`; `MinSparseScore` cuts a `SearchSparse` as well as the sparse half of a hybrid one. (An earlier `MinScore` property applied only to the dense-only path and has been removed.)

One difference remains in *where* the cut happens. On the dense-only path the index returns the top-k first and the cut is applied to those: with `top: 10` and a high `MinDenseScore` you can get fewer than 10 hits even if more documents above the threshold exist further down the ranking. On the sparse side the cut is a real threshold inside the engine, which prunes and still fills k when enough documents pass.

### Hybrid from a sentence (the server derives both halves)

A sentence carries no vectors and no sparse terms: the server embeds it densely *and* derives the sparse terms from the same text with the model's tokenizer, then fuses the two halves. There is no flag to turn that off.

```csharp
var results = articles.Search("how do I reset my password?", top: 10);

// The same, with a predicate.
var news = articles.Search("password reset", predicate: a => a.Category == "news", top: 10);
```

The same rule holds on writes: `articles.Add(42, article, sentence: "...")` stores both slots from one server-side tokenizer pass, and no overload accepts sparse terms together with a sentence. See [Sparse index](../indexes/sparse.md#producing-the-pairs).

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

The predicate is a LINQ expression tree; it is translated client-side into the gRPC filter AST (`FilterNode`/`PropertyEqualsCondition`/`PropertyCollectionAnyCondition`/`LogicalCondition`, see [gRPC API](../server/grpc-api.md)) and evaluated server-side against the stored document. Supported shapes are property equality (`x.Prop == value`), collection membership (`x.Tags.Any(t => t == value)`), and `&&`/`||` combinations of those — the same subset documented for [in-process filtering](../in-process/collections.md). The same predicate parameter is available on the vector-based `SearchDense(embeddings, predicate, ...)` and on the hybrid `Search`.

The sentence forms of `Search` require the server to have an embedding module; the vector forms never do. `SearchSparse` needs no embedding module at all.

### By example (embedding of an existing document)

`GetEmbeddings`/`GetEmbeddingsAsync` read back every stored vector of a key (null when the key is missing or the entry has no vector) — which makes "more like this" a two-liner without an embedding module:

```csharp
var vectors = await articles.GetEmbeddingsAsync(42);
var similar = await articles.SearchDenseAsync(vectors[0], top: 10); // one query vector per search
```

### Per-query tuning

Every search overload accepts an optional `SearchOptions`:

```csharp
var results = await articles.SearchDenseAsync(myFloatVector, top: 10, options: new SearchOptions
{
  EfSearch = 200,        // HNSW beam width for THIS query (recall vs latency); 0 = server default
  NoContent = true,      // keys and scores only: results come back with Content == null
  MinDenseScore = 0.30f, // drop the weak dense hits: dense-only or hybrid
  MinSparseScore = 0.05f // drop the weak sparse hits: sparse-only or hybrid
});
```

`EfSearch` is the knob to recover recall on large collections without changing the server-wide default; it is ignored by exact (brute force) indexes.

### Streaming keys

`Keys`/`GetKeysAsync` return every key in one response, which can exceed the gRPC message limit on large collections. `StreamKeysAsync` streams them in chunks of 1000 (configurable):

```csharp
await foreach (var key in articles.StreamKeysAsync())
  Process(key);
```

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
