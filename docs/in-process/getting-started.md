# Getting started

This page walks through installing `Jigen.Store` and writing a minimal application that creates a database, indexes a few vectors, and searches them.

## Install

```bash
dotnet add package Jigen.Store
```

Brute-force (exact) search works out of the box. To use the approximate HNSW index instead, also add:

```bash
dotnet add package Jigen.Indexer.HNSW
```

Both packages target `net10.0`.

## One Store = one database

A `Store` instance owns a set of files under a directory (`DataBasePath`) named after `DataBaseName` (see [Store options](store-options.md) for the full file layout). A database can be opened by only one `Store` instance at a time, in one process: the constructor takes an exclusive lock file, and a second attempt to open the same path throws an `IOException`.

```csharp
using Jigen;
using Jigen.DataStructures;

var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo"
  // Indexer defaults to BruteForceIndexer; see below for HNSW.
});
```

## Writing entries

The lowest-level write path is `AppendContent` with a `VectorEntry`: a key (`byte[]`), a collection name, a serialized content payload, and an embedding.

```csharp
using Jigen.Extensions;

await store.AppendContent(new VectorEntry
{
  Id = Guid.NewGuid().ToByteArray(),
  CollectionName = "articles",
  Content = MessagePackDocumentSerializer.Instance.Serialize("Hello, Jigen!"),
  Embedding = embeddingVector // float[] or ReadOnlyMemory<float>
});
```

For typed access, wrap the store in a `VectorCollection<T>` instead — see [Collections](collections.md) for the full surface (`IDictionary`-like API, typed content, custom serializers):

```csharp
public class Article
{
  public string Title { get; set; }
  public string Body { get; set; }
}

var articles = store.VectorCollection<Article>("articles");

articles.Add(Guid.NewGuid(), new VectorEntry<Article>
{
  Content = new Article { Title = "Hello", Body = "Hello, Jigen!" },
  Embedding = embeddingVector
});
```

## Searching

```csharp
var results = store.Search("articles", queryVector, top: 5);

foreach (var (entry, score) in results)
{
  var article = MessagePackDocumentSerializer.Instance.Deserialize<Article>(entry.Content);
  Console.WriteLine($"{article.Title} (score {score})");
}
```

`Search` ranks by cosine similarity, whether the collection uses the brute-force or the HNSW index — see [Brute-force index](../indexes/brute-force.md) and [HNSW index](../indexes/hnsw.md).

## Durability and shutdown

Writes are asynchronous (see [Store options](store-options.md#durability-model)): call `SaveChangesAsync` to force a checkpoint (fsync of content, embeddings, index, and the index graph) and to observe any background ingestion failure. Always close the store when done, so the exclusive lock is released cleanly:

```csharp
await store.SaveChangesAsync();
await store.Close();       // or: store.Dispose();
```

A `Store` also implements `IDisposable`, which calls `Close()` synchronously — convenient for `using` blocks, at the cost of blocking the calling thread on shutdown.

## Using HNSW instead of brute force

Swap the indexer in `StoreOptions`:

```csharp
using Jigen.Indexer;

var store = new Store(new StoreOptions
{
  DataBasePath = "/data/jigendb",
  DataBaseName = "demo",
  Indexer = new SmallWorldIndexer(new SmallWorldOptions
  {
    M = 16,
    EfConstruction = 200,
    EfSearch = 50,
    StoragePath = "/data/jigendb/hnsw"
  })
});
```

See [HNSW index](../indexes/hnsw.md) for the full parameter reference and tuning guidance.
