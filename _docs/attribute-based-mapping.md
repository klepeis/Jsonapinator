# Attribute-based mapping

The default way to map a POCO to a JSON:API resource: decorate the class and its properties with
`Jsonapinator.Attributes`, then use `new JsonApiSerializer()`. Every property mapping is explicit
— nothing is inferred from naming or shape.

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

var serializer = new JsonApiSerializer();
string json = serializer.Serialize(article);
Article roundTripped = serializer.Deserialize<Article>(json);
```

See [`../README.md`](../README.md) for `JsonApiSerializer`'s full method list (`SerializeCollection`,
`SerializeErrors`, `DeserializeCollection`, `BuildDocument`/`ParseDocument`,
`JsonApiDocumentOptions`) — this document only covers how a type gets mapped in the first place.
See [`../samples/Jsonapinator.Sample.AttributeBased`](../samples/Jsonapinator.Sample.AttributeBased)
for a runnable ASP.NET Core example.

## When to use this over convention-based mapping

- You want the JSON:API resource type name to be something other than the camelCase class name
  (e.g. `"articles"`, plural, rather than `"article"`).
- You want to be explicit and self-documenting about exactly which properties are exposed,
  independent of their shape — the attributes double as inline documentation.
- Your POCOs don't follow the `Id`-named-property convention (see
  [convention-based-mapping.md](convention-based-mapping.md)) and you don't want to rename them.

If none of that matters to you, [convention-based mapping](convention-based-mapping.md) gets you
the same behavior with no attributes at all.

## Attribute reference

All four attributes live in the `Jsonapinator.Attributes` namespace. `[JsonApiResource]` and
`[JsonApiId]` are required on every mapped type; `[JsonApiAttribute]` and `[JsonApiRelationship]`
are opt-in per property — an unmarked property is simply never serialized/deserialized, not an
error.

| Attribute | Target | Required | Description |
|---|---|---|---|
| `[JsonApiResource(string resourceType)]` | class | Yes, exactly one | Declares the JSON:API resource type name (the `"type"` member) for the class. |
| `[JsonApiId]` | property | Yes, exactly one | Marks the property that supplies the resource's `"id"`. Supported CLR types: `string`, `Guid`, `int`, `long` — always serialized as a JSON string per spec, parsed back to the declared type on deserialize. |
| `[JsonApiAttribute]` | property | No, one per attribute | Marks a property as a JSON:API `"attributes"` member. JSON name defaults to camelCase of the property name; override with `[JsonPropertyName]`. |
| `[JsonApiRelationship(string name, RelationshipKind kind)]` | property | No, one per relationship | Marks a navigation property as a JSON:API `"relationships"` member. `kind` is `RelationshipKind.ToOne` (property holds a single related object or `null`) or `RelationshipKind.ToMany` (property holds `List<T>` or `T[]`). |

### `[JsonApiResource]`

Put it on the class, passing the resource type name used in JSON:API URLs and the `"type"` member
(conventionally plural, e.g. `"articles"`, but any string works):

```csharp
[JsonApiResource("articles")]
public class Article { /* ... */ }
```

### `[JsonApiId]`

Exactly one property per class. Any of the four supported id types works — the value is always
serialized as a JSON string per spec, and parsed back to the declared CLR type on deserialize:

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

### `[JsonApiAttribute]`

Put it on each scalar/value property you want serialized. A property without this attribute is
silently skipped — useful for internal/computed fields you don't want exposed:

```csharp
public class Article
{
    [JsonApiAttribute]
    public string Title { get; set; } = "";

    // Not marked -> never appears in the JSON:API document.
    public DateTime LastIndexedAtUtc { get; set; }
}
```

JSON names default to camelCase; override with the standard
`[System.Text.Json.Serialization.JsonPropertyName]` attribute:

```csharp
[JsonApiAttribute]
[JsonPropertyName("word-count")]
public int WordCount { get; set; }
```

### `[JsonApiRelationship]`

Put it on navigation properties. The property's CLR type (or its element type, for to-many) must
itself be `[JsonApiResource]`-decorated:

```csharp
public class Article
{
    [JsonApiRelationship("author", RelationshipKind.ToOne)]
    public Person? Author { get; set; }

    [JsonApiRelationship("comments", RelationshipKind.ToMany)]
    public List<Comment> Comments { get; set; } = new();
}
```

For fetching the related resources' own attributes into the response (not just their id/type),
see [compound-documents.md](compound-documents.md).

## Document-level `meta`/`links`

The top-level JSON:API document's own `meta`/`links` (e.g. pagination info, a top-level `self`
link) aren't POCO-driven at all — they're supplied per call via `JsonApiDocumentOptions`, passed
to `Serialize`/`SerializeCollection`:

```csharp
var options = new JsonApiDocumentOptions
{
    Meta = new MetaObject { ["requestId"] = "abc-123" },
    Links = new LinksObject { ["self"] = "/articles/1" },
};

string json = serializer.Serialize(article, options);
```

This is the same mechanism regardless of mapping mode. See
[`../samples/Jsonapinator.Sample.AttributeBased/ArticlesController.cs`](../samples/Jsonapinator.Sample.AttributeBased/ArticlesController.cs)'s
`with-document-meta` action for a runnable example.

## Resource-level and relationship-level `meta`/`links`

Unlike document-level `meta`/`links`, these live directly on the POCO — no
`JsonApiDocumentOptions` needed, they're populated automatically on every `Serialize`/
`Deserialize` call.

| Attribute | Target | Required | Description |
|---|---|---|---|
| `[JsonApiMeta]` | property of type `MetaObject` | No, at most one | Lifts the property's value onto the resource object's own `"meta"`. |
| `[JsonApiLinks]` | property of type `LinksObject` | No, at most one | Lifts the property's value onto the resource object's own `"links"`. |
| `[JsonApiRelationshipMeta(string relationshipName)]` | property of type `MetaObject` | No, at most one per relationship | Lifts the property's value onto the named relationship object's own `"meta"`. |
| `[JsonApiRelationshipLinks(string relationshipName)]` | property of type `LinksObject` | No, at most one per relationship | Lifts the property's value onto the named relationship object's own `"links"`. |

`relationshipName` must match the `name` passed to `[JsonApiRelationship(name, kind)]` on the
actual relationship property — `Resolve` throws `JsonApiMappingException` if it doesn't match any
declared relationship, or if the attributed property's type isn't exactly `MetaObject`/
`LinksObject`.

```csharp
[JsonApiResource("articles")]
public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiRelationship("comments", RelationshipKind.ToMany)]
    public List<Comment> Comments { get; set; } = new();

    // The resource object's own "meta"/"links" — e.g. { "data": { ..., "meta": {...}, "links": {...} } }.
    [JsonApiMeta]
    public MetaObject? ArticleMeta { get; set; }

    [JsonApiLinks]
    public LinksObject? ArticleLinks { get; set; }

    // The "comments" relationship object's own "meta"/"links" — distinct from any individual
    // Comment's meta/links, and distinct from ArticleMeta/ArticleLinks above. One meta/links
    // object per relationship as a whole, matching the JSON:API relationship-object shape (not
    // per related resource).
    [JsonApiRelationshipMeta("comments")]
    public MetaObject? CommentsMeta { get; set; }

    [JsonApiRelationshipLinks("comments")]
    public LinksObject? CommentsLinks { get; set; }
}
```

On deserialize, mapping is presence-based like attributes and relationships: if the incoming JSON
omits a resource's/relationship's `meta`/`links`, the corresponding POCO property is left
untouched (so a PATCH-style partial payload can't clobber it).

See
[`../samples/Jsonapinator.Sample.AttributeBased/Article.cs`](../samples/Jsonapinator.Sample.AttributeBased/Article.cs)
for the full runnable example.

## Per-instance `"type"` override — `[JsonApiType]`

`[JsonApiResource(string resourceType)]` declares a single, static resource type name for every
instance of a class. `[JsonApiType]` layers a per-instance override on top of that default —
useful for a discriminator-style CLR type shared by several JSON:API resource types (e.g. one
`Attachment` class that emits `"videos"` for some instances and `"images"` for others, based on
data rather than CLR type):

```csharp
[JsonApiResource("attachments")]
public class Attachment
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiType]
    public string? AttachmentType { get; set; }
}
```

- The property must be of type exactly `string`; `Resolve` throws `JsonApiMappingException` if
  another type is used, or if more than one property is decorated with `[JsonApiType]`.
- When the property's runtime value is null or empty for a given instance, the normal
  `[JsonApiResource]` name is used instead — the override is opt-in per instance, not a
  replacement for the class-level default.
- On deserialize, the property is populated from the incoming resource's actual `"type"` string
  (for the primary resource, and for every related resource reached via a relationship —
  including identifier-only stubs when no matching entry appears in `"included"`).

See
[`../samples/Jsonapinator.Sample.AttributeBased/Article.cs`](../samples/Jsonapinator.Sample.AttributeBased/Article.cs)'s
`Attachment` class for the full runnable example.
