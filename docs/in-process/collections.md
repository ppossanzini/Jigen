# Collections

A collection is just a name shared by a group of entries in a `Store` — there is no explicit "create collection" call. `Jigen.Store` provides two typed wrappers on top of the raw `Store` API: `VectorCollection<T>` for content with an embedding, and `DocumentCollection<T>` for content-only data with LINQ-predicate search.

Both are obtained from a `Store` via extension methods:

```csharp
var articles = store.VectorCollection<Article>("articles");   // explicit name
var articles2 = store.VectorCollection<Article>();             // name defaults to typeof(T).Name

var logs = store.DocumentCollection<LogEntry>("logs");
```

## VectorCollection\<T\>

`VectorCollection<T>` wraps a `Store` collection whose entries carry both a typed content payload and an embedding. It implements `IDictionary<VectorKey, VectorEntry<T>>`:

```csharp
public class VectorEntry<T> where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }
  public float[] Embedding { get; set; }
}
```

```csharp
var articles = store.VectorCollection<Article>("articles");

articles.Add(Guid.NewGuid(), new VectorEntry<Article>
{
  Content = new Article { Title = "Hello" },
  Embedding = embeddingVector
});

articles.ContainsKey(key);
articles.TryGetValue(key, out var entry);
articles.Remove(key);
var count = articles.Count;
foreach (var kv in articles) { /* key, VectorEntry<T> */ }
```

`Add`/indexer-set calls go through `Store.AppendContent` and complete synchronously from the caller's point of view (they block on the async ingestion call) — see the durability model in [Store options](store-options.md#durability-model) for what "written" guarantees at that point.

### VectorCollectionOptions\<T\>

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Dimensions` | `int` | `1536` | Expected embedding size. Informational for the collection; not enforced by the collection wrapper itself. |
| `Name` | `string` | `typeof(T).Namespace + "." + typeof(T).Name` | Collection name in the underlying store. |
| `DocumentSerializer` | `IDocumentSerializer` | `MessagePackDocumentSerializer.Instance` | Serializes/deserializes `T` to/from the content payload. |

```csharp
var articles = new VectorCollection<Article>(store, new VectorCollectionOptions<Article>
{
  Name = "articles",
  Dimensions = 768,
  DocumentSerializer = MyCustomSerializer.Instance
});
```

## DocumentCollection\<T\>

`DocumentCollection<T>` wraps content-only data (no embedding) and implements `IDictionary<VectorKey, T>` directly — `T` itself is the value, not a wrapper. It adds a `Search` overload that filters by a LINQ predicate translated to a server/index-level filter, without deserializing every document:

```csharp
var logs = store.DocumentCollection<LogEntry>("logs");

logs.Add(Guid.NewGuid(), new LogEntry { Category = "auth", Message = "login ok" });

List<KeyValuePair<VectorKey, LogEntry>> results =
  logs.Search(x => x.Category == "auth");
```

`DocumentCollection<T>.Search` also accepts a pre-built `IFilterExpression` (the same AST the predicate is translated to), which is useful when the filter needs to travel across a process boundary (e.g. to a gRPC server) without re-parsing an expression tree.

### DocumentCollectionOptions\<T\>

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Name` | `string` | `typeof(T).Namespace + "." + typeof(T).Name` | Collection name in the underlying store. |
| `DocumentSerializer` | `IDocumentSerializer` | `MessagePackSerializedDocumentFilter.Instance` | Serializer used for content; also implements `ISerializedDocumentFilter`, letting filters match against the serialized bytes without a full deserialize. |

## VectorKey

Both collections key entries with `VectorKey`, a value type wrapping a `byte[]` with correct value-based equality and hashing (`XxHash32`). It has implicit conversions from the common key types, so callers rarely construct one explicitly:

```csharp
VectorKey k1 = 42;                 // int
VectorKey k2 = 42L;                // long
VectorKey k3 = Guid.NewGuid();      // Guid
VectorKey k4 = "my-key";            // string (UTF-8 bytes)
VectorKey k5 = someByteArray;       // byte[]
```

Supported implicit conversions: `int`, `uint`, `long`, `ulong`, `Guid`, `string`, `byte[]`.

## Filtering

Predicates passed to `DocumentCollection<T>.Search` are translated into an `IFilterExpression` AST (`Jigen.Filtering`) rather than executed as compiled .NET code. This is what lets the same filter be evaluated locally against the serialized document, or serialized itself and shipped to a remote server for server-side evaluation (see the client's [filtering](../client/usage.md)).

Supported predicate shapes:

| Pattern | Example |
|---|---|
| Property equality | `x => x.Category == "auth"` |
| Collection membership | `x => x.Tags.Any(t => t == "urgent")` |
| Logical AND | `x => x.Category == "auth" && x.Lang == "en"` |
| Logical OR | `x => x.Category == "auth" \|\| x.Category == "billing"` |
| Combinations of the above | `x => x.Tags.Any(t => t == "ai") && x.Lang == "en"` |

The filter is evaluated against the document's MessagePack payload converted to JSON, matching against `int`, `long`, `double`, `bool`, `string`, and `null` values.

### Limits

- Only equality (`==`) comparisons are translated; relational operators (`<`, `>`, `!=`), string operations (`Contains`, `StartsWith`), and arbitrary method calls are not supported by the expression translator.
- Property paths are limited to member-access chains (`x.Prop`, `x.Nested.Prop`); the right-hand side of a comparison must be a constant or a captured local/field/property, not another document property.
- Without a filter, brute-force search picks the top-k directly from ranked scores; with a filter, both the brute-force and HNSW indexers rank/traverse candidates first and then apply the filter, backfilling until `top` matching results are found or candidates are exhausted — so a highly selective filter over a large collection costs more than an unfiltered search.

## See also

- [Store options](store-options.md) — durability, files on disk, shrink.
- [Brute-force index](../indexes/brute-force.md) and [HNSW index](../indexes/hnsw.md) — how `Search` is actually executed.
