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
