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
