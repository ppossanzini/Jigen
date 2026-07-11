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

The predicate is a LINQ expression tree; it is translated client-side into the gRPC filter AST (`FilterNode`/`PropertyEqualsCondition`/`PropertyCollectionAnyCondition`/`LogicalCondition`, see [gRPC API](../server/grpc-api.md)) and evaluated server-side against the stored document. Supported shapes are property equality (`x.Prop == value`), collection membership (`x.Tags.Any(t => t == value)`), and `&&`/`||` combinations of those — the same subset documented for [in-process filtering](../in-process/collections.md).

`Search(sentence, ...)` always requires the server to have an embedding module; `Search(embeddings, top)` never does.

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
