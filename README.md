# Jsonapinator

A framework-agnostic .NET 8 class library implementing the core [JSON:API](https://jsonapi.org/format/)
document structure — for use by ASP.NET Core Web API projects (or any .NET code) that need to
produce and consume spec-compliant JSON:API documents.

Built test-first (TDD) following SOLID design principles.

## Install

Reference the `src/Jsonapinator/Jsonapinator.csproj` project (a NuGet package is not yet
published).

## Quick start

```csharp
using Jsonapinator;
using Jsonapinator.Attributes;

[JsonApiResource("articles")]
public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Title { get; set; } = "";
}

var serializer = new JsonApiSerializer();
string json = serializer.Serialize(new Article { Id = "1", Title = "Hello" });
Article article = serializer.Deserialize<Article>(json);
```

## Two ways to map a POCO

| | Setup | Best for |
|---|---|---|
| [**Attribute-based**](_docs/attribute-based-mapping.md) (default) | `new JsonApiSerializer()` | Explicit control over resource type names and which properties are exposed. |
| [**Convention-based**](_docs/convention-based-mapping.md) | `JsonApiSerializer.WithConventions()` | Plain POCOs with zero `Jsonapinator.Attributes` — an `Id`-named property becomes the id, id-bearing property types become relationships, everything else becomes an attribute. |

Both produce and consume the exact same `JsonApiSerializer` API below — they only differ in how a
CLR type's `ResourceMetadata` gets built.

## `JsonApiSerializer` API

```csharp
string json = serializer.Serialize(article);
string collectionJson = serializer.SerializeCollection(articles);
string errorsJson = serializer.SerializeErrors(errors);

Article article = serializer.Deserialize<Article>(json);
IReadOnlyList<Article> articles = serializer.DeserializeCollection<Article>(collectionJson);
```

For finer control (document-level `meta`/`links`, [compound documents](_docs/compound-documents.md),
or working with the spec-shaped document model directly), use `BuildDocument`/`ParseDocument` or
`JsonApiDocumentOptions`:

```csharp
var options = new JsonApiDocumentOptions
{
    Meta = new MetaObject { ["copyright"] = "Copyright 2026" },
    Links = new LinksObject { ["self"] = "http://example.com/articles/1" },
};

string json = serializer.Serialize(article, options);
```

See [`_docs/compound-documents.md`](_docs/compound-documents.md) for populating and consuming a
top-level `"included"` array via `JsonApiDocumentOptions.Include`.

## ASP.NET Core integration

`Jsonapinator.AspNetCore` (a separate sibling project — core `Jsonapinator` stays
dependency-free) makes `application/vnd.api+json` work automatically for ASP.NET Core Web API
controllers, via custom MVC input/output formatters registered on startup:

```csharp
builder.Services.AddControllers().AddJsonApi();
```

Once registered, controller actions return/accept plain POCOs like any other action — no
per-action code or `[Produces]`/`[Consumes]` attributes needed:

```csharp
[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    [HttpGet("{id}")]
    public Article Get(string id) => /* ... */;

    [HttpPost]
    public Article Post([FromBody] Article article) => /* ... */;
}
```

A request with `Accept: application/vnd.api+json` gets back a JSON:API-shaped response; a
request with `Content-Type: application/vnd.api+json` has its body deserialized automatically. A
malformed body, validation failures, and unhandled exceptions all map to JSON:API error documents
too, when the client negotiated `application/vnd.api+json` — see
[Error documents](_docs/aspnetcore-integration.md#error-documents) for the negotiation-aware
default, the `MapErrorsAlways()` override, and the one extra `app.UseExceptionHandler()` call
required for unhandled-exception mapping.

`AddJsonApi()` defaults to **convention-based** mapping (the opposite default from
`new JsonApiSerializer()` above) — call `AddJsonApi(options => options.UseAttributes())` for
attribute-based mapping instead.

**See [`_docs/aspnetcore-integration.md`](_docs/aspnetcore-integration.md) for the full setup**
(how it works, malformed-body handling, known limitations), or run one of the samples below for a
working example.

## Samples

| Project | Demonstrates |
|---|---|
| [`samples/Jsonapinator.Sample.ConventionBased`](samples/Jsonapinator.Sample.ConventionBased) | Convention-based mapping: to-one/to-many relationships, nested object/array attributes, a `Guid`-keyed resource, and the `Include`/compound-documents escape hatch. |
| [`samples/Jsonapinator.Sample.AttributeBased`](samples/Jsonapinator.Sample.AttributeBased) | The same resource graph mapped via explicit `Jsonapinator.Attributes`, including a `[JsonPropertyName]` override. |
| [`samples/Jsonapinator.Sample.ErrorHandling.Default`](samples/Jsonapinator.Sample.ErrorHandling.Default) | The negotiation-aware error-mapping default — JSON:API errors only when `Accept: application/vnd.api+json` was sent. |
| [`samples/Jsonapinator.Sample.ErrorHandling.AlwaysMap`](samples/Jsonapinator.Sample.ErrorHandling.AlwaysMap) | `options.MapErrorsAlways()` — JSON:API errors regardless of what was negotiated. |

Each sample's `Program.cs` has `curl` commands in a header comment showing what to try.

## Known V1 limitations

- Deserialized relationships are identifier-only stubs unless the source JSON has a matching
  entry in its top-level `"included"` array, in which case they're fully hydrated — see
  [`_docs/compound-documents.md`](_docs/compound-documents.md).
- To-many relationship properties must be `List<T>` or `T[]`.
- Resource ids are limited to `string`, `Guid`, `int`, and `long`.

## Development

```
dotnet build
dotnet test
```
