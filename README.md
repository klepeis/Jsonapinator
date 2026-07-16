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

### Attribute reference

All four attributes live in the `Jsonapinator.Attributes` namespace. `[JsonApiResource]` and
`[JsonApiId]` are required on every mapped type; `[JsonApiAttribute]` and `[JsonApiRelationship]`
are opt-in per property (V1 does not map properties by convention — an unmarked property is
simply never serialized/deserialized).

| Attribute | Target | Required | Description |
|---|---|---|---|
| `[JsonApiResource(string resourceType)]` | class | Yes, exactly one | Declares the JSON:API resource type name (the `"type"` member) for the class. |
| `[JsonApiId]` | property | Yes, exactly one | Marks the property that supplies the resource's `"id"`. Supported CLR types: `string`, `Guid`, `int`, `long` — always serialized as a JSON string per spec, parsed back to the declared type on deserialize. |
| `[JsonApiAttribute]` | property | No, one per attribute | Marks a property as a JSON:API `"attributes"` member. JSON name defaults to camelCase of the property name; override with `[JsonPropertyName]`. |
| `[JsonApiRelationship(string name, RelationshipKind kind)]` | property | No, one per relationship | Marks a navigation property as a JSON:API `"relationships"` member. `kind` is `RelationshipKind.ToOne` (property holds a single related object or `null`) or `RelationshipKind.ToMany` (property holds `List<T>` or `T[]`). |

**`[JsonApiResource]`** — put it on the class, passing the plural resource type name used in JSON:API URLs and the `"type"` member:

```csharp
[JsonApiResource("articles")]
public class Article { /* ... */ }
```

**`[JsonApiId]`** — exactly one property per class. Any of the four supported id types works:

```csharp
public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";
}

public class Order
{
    [JsonApiId]
    public Guid Id { get; set; } // serializes as e.g. "11111111-1111-1111-1111-111111111111"
}
```

**`[JsonApiAttribute]`** — put it on each scalar/value property you want serialized. A property
without this attribute is silently skipped (not an error) — useful for internal/computed fields
you don't want exposed:

```csharp
public class Article
{
    [JsonApiAttribute]
    public string Title { get; set; } = "";

    // Not marked -> never appears in the JSON:API document.
    public DateTime LastIndexedAtUtc { get; set; }
}
```

**`[JsonApiRelationship]`** — put it on navigation properties. The property's CLR type (or its
element type, for to-many) must itself be `[JsonApiResource]`-decorated:

```csharp
public class Article
{
    [JsonApiRelationship("author", RelationshipKind.ToOne)]
    public Person? Author { get; set; }

    [JsonApiRelationship("comments", RelationshipKind.ToMany)]
    public List<Comment> Comments { get; set; } = new();
}
```

### Convention-based mapping (no attributes)

If you'd rather not decorate every POCO, use `JsonApiSerializer.WithConventions()` instead of
`new JsonApiSerializer()`. It maps plain POCOs by convention, no `Jsonapinator.Attributes`
required:

```csharp
public class Article
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public Person? Author { get; set; }
    public List<Comment> Comments { get; set; } = new();
}

public class Person
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
}

var serializer = JsonApiSerializer.WithConventions();
string json = serializer.Serialize(article);
```

The classification rule, applied to every public property with both a getter and a setter
(indexers and get-only/set-only properties are silently skipped, never mapped):

1. A property literally named `Id` (of type `string`, `Guid`, `int`, or `long`) becomes the
   resource id. Exactly one is required, or `JsonApiSerializer` throws `JsonApiMappingException`.
2. The resource type name (the `"type"` member) is the camelCase class name, **not pluralized**
   — `Article` → `"article"`, `OrderLine` → `"orderLine"`. English pluralization is unreliable
   (`Person` → `"People"`, not `"Persons"`), so this mode deliberately doesn't attempt it; use
   attribute-based mapping with `[JsonApiResource("people")]` if you need a specific type name.
3. A property whose type (or element type, for `List<T>`/`T[]`) is itself a class with its own
   usable `Id` property becomes a relationship — to-one for a single reference, to-many for a
   collection.
4. Everything else (primitives, `string`, `Guid`, `DateTime`, `decimal`, enums, nested objects/
   collections without an `Id` property) becomes a flat attribute, serialized as-is — including
   nested objects and arrays, matching "assume all elements of the POCO are present."

`[JsonPropertyName]` overrides are still respected for attribute names in convention mode, same
as attribute-based mapping. Convention mode and attribute mode are mutually exclusive per
`JsonApiSerializer` instance — a type is resolved entirely by whichever resolver that instance
was built with, no mixing.

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
top-level `"included"` array. Related objects are read via plain reflection
(`PropertyInfo.GetValue`) directly off the POCO's relationship properties — Jsonapinator has no
dependency on Entity Framework Core or any other ORM/database library (the core project has zero
`PackageReference`s beyond the .NET 8 BCL) and does not know or care where the object graph came
from. It simply assumes the relationship properties are already populated in memory by the time
`Serialize` is called (e.g. via EF Core `.Include()`, a manual query, an in-memory fixture, etc.)
— it does not lazy-load anything itself:

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
