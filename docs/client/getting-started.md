# Client: getting started

`Jigen.Client` is the gRPC client library for talking to a Jigen server (see [server overview](../server/overview.md)) from a .NET application. It targets `net8.0` and above.

## Install

```bash
dotnet add package Jigen.Client
```

## Connect

Create a `ConnectionOptions` and a `Context`:

```csharp
using Jigen.Client;

var options = new ConnectionOptions
{
  HostName = "localhost",
  Port = 3223,
  TLS = false,
  DatabaseName = "mydb"
};

var context = new Context(options);
```

### `ConnectionOptions`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `HostName` | string | `localhost` | Server host name or IP. |
| `Port` | int | `3223` | Server gRPC port. |
| `TLS` | bool | `false` | Use HTTPS/TLS for the gRPC channel. When `false`, unencrypted HTTP/2 is explicitly enabled on the client. |
| `AllowUntrustedServerCertificate` | bool | `false` | Skip server certificate validation (development/self-signed certificates only). |
| `DatabaseName` | string | *(required)* | Name of the database this context operates on. Required before any collection call — an `InvalidOperationException` is thrown otherwise. |

## Typed collections

A `Context` exposes vector data through `VectorCollection<T>`, one per collection:

```csharp
using Jigen.Client;

public class Article
{
  public string Title { get; set; }
  public string Category { get; set; }
  public string[] Tags { get; set; }
}

var articles = new VectorCollection<Article>(context);
```

By default the collection name is `typeof(T).Namespace + "." + typeof(T).Name`; pass a `VectorCollectionOptions<T>` to override it or the document serializer:

```csharp
var articles = new VectorCollection<Article>(context, new VectorCollectionOptions<Article>
{
  Name = "articles"
});
```

## Recommended pattern: subclass `Context`

For an application with several collections, subclass `Context` and override `ContextBuilder()` to expose them as properties. This centralizes collection names/options and gives call sites a single injectable object:

```csharp
using Jigen.Client;

public class DB : Context
{
  public VectorCollection<Article> Articles { get; private set; }
  public VectorCollection<Product> Products { get; private set; }

  public DB(ConnectionOptions options) : base(options) { }

  protected override void ContextBuilder()
  {
    Articles = new VectorCollection<Article>(this, new VectorCollectionOptions<Article> { Name = "articles" });
    Products = new VectorCollection<Product>(this, new VectorCollectionOptions<Product> { Name = "products" });
  }
}
```

```csharp
var db = new DB(new ConnectionOptions { DatabaseName = "mydb" });
db.Articles.Add(42, new Article { Title = "Jigen DB" }, "Jigen is a vector database written in C#.");
```

`ContextBuilder()` runs at the end of the base constructor, so all fields assigned there are ready to use as soon as the object is constructed.

For inserting, searching and filtering, see [client usage](usage.md).
