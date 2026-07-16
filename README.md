# Jsonapinator

A framework-agnostic .NET 8 class library implementing the core [JSON:API](https://jsonapi.org/format/)
document structure — for use by ASP.NET Core Web API projects (or any .NET code) that need to
produce and consume spec-compliant JSON:API documents.

Built test-first (TDD) following SOLID design principles. See
[`_docs/future-roadmap.md`](_docs/future-roadmap.md) for what's deferred to later phases
(sparse fieldsets, sorting, pagination, filtering, extensions, atomic operations).

## Install

Reference the `src/Jsonapinator/Jsonapinator.csproj` project (a NuGet package is not yet
published).

## Usage

Map a POCO to a JSON:API resource with attributes:

```csharp
using Jsonapinator.Attributes;

[JsonApiResource("articles")]
public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Title { get; set; } = "";

    [JsonApiRelationship("author", RelationshipKind.ToOne)]
    public Person? Author { get; set; }

    [JsonApiRelationship("comments", RelationshipKind.ToMany)]
    public List<Comment> Comments { get; set; } = new();
}
```

Serialize and deserialize via `JsonApiSerializer`:

```csharp
using Jsonapinator;

var serializer = new JsonApiSerializer();

string json = serializer.Serialize(article);
string collectionJson = serializer.SerializeCollection(articles);
string errorsJson = serializer.SerializeErrors(errors);

Article article = serializer.Deserialize<Article>(json);
IReadOnlyList<Article> articles = serializer.DeserializeCollection<Article>(collectionJson);
```

Attribute JSON names default to camelCase; override with the standard
`[System.Text.Json.Serialization.JsonPropertyName]` attribute:

```csharp
[JsonApiAttribute]
[JsonPropertyName("word-count")]
public int WordCount { get; set; }
```

For finer control (e.g. attaching document-level `meta`/`links`, or working with the spec-shaped
document model directly), use `BuildDocument`/`ParseDocument` or `JsonApiDocumentOptions`:

```csharp
var options = new JsonApiDocumentOptions
{
    Meta = new MetaObject { ["copyright"] = "Copyright 2026" },
    Links = new LinksObject { ["self"] = "http://example.com/articles/1" },
};

string json = serializer.Serialize(article, options);
```

### Compound documents (`include`)

Pass dot-notation relationship paths via `JsonApiDocumentOptions.Include` to populate the
top-level `"included"` array. Related objects are read directly off the POCO's relationship
properties, which are assumed to already be loaded (e.g. via EF Core `.Include()` before calling
`Serialize`) — Jsonapinator does not lazy-load them itself:

```csharp
var options = new JsonApiDocumentOptions { Include = new[] { "author", "comments.author" } };
string json = serializer.Serialize(article, options);
```

On deserialize, when the source JSON has an `"included"` array, matching relationships are fully
hydrated (attributes and their own nested relationships populated) instead of being left as
id-only stubs — this happens automatically, no extra API call needed:

```csharp
Article article = serializer.Deserialize<Article>(json);
article.Author!.FirstName // populated if "author" was present in "included"
```

### Known V1 limitations

- Deserialized relationships are identifier-only stubs unless the source JSON has a matching
  entry in its top-level `"included"` array, in which case they're fully hydrated.
- To-many relationship properties must be `List<T>` or `T[]`.
- Resource ids are limited to `string`, `Guid`, `int`, and `long`.
- No query-string `?include=...` parsing helper is provided — consumers turn that into an
  `IEnumerable<string>` of paths themselves.

## Development

```
dotnet build
dotnet test
```
