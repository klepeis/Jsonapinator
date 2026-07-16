# Polymorphism (`System.Text.Json` `[JsonPolymorphic]`/`[JsonDerivedType]`)

Jsonapinator leverages the plain BCL `System.Text.Json.Serialization` polymorphism attributes
(introduced .NET 7) — no Jsonapinator-specific attribute vocabulary of its own. Works identically
under [attribute-based](attribute-based-mapping.md) and [convention-based](convention-based-mapping.md)
mapping.

## Single-valued polymorphic attribute values

A `[JsonApiAttribute]` (or convention-classified flat) property whose declared type is a
polymorphic base class round-trips correctly, discriminator and all:

```csharp
[JsonPolymorphic]
[JsonDerivedType(typeof(Circle), "circle")]
[JsonDerivedType(typeof(Square), "square")]
public abstract class Shape { }

public class Circle : Shape { public double Radius { get; set; } }
public class Square : Shape { public double Side { get; set; } }

public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public Shape? FeaturedShape { get; set; }
}
```

```json
{ "data": { "type": "articles", "id": "1", "attributes": {
    "featuredShape": { "$type": "circle", "Radius": 5 }
} } }
```

Serializing `article.FeaturedShape = new Circle { Radius = 5 }` embeds the `"$type"` discriminator
(or whatever `TypeDiscriminatorPropertyName` is configured to), and deserializing resolves the
correct concrete `Circle`/`Square` instance back out. `List<Shape>`/`Shape[]`-typed attributes
already worked correctly before this — only single-valued polymorphic properties needed a fix
(`ResourceGraphBuilder`/`JsonApiDocumentWriter` now serialize such a value via its **declared**
type, not its runtime type, so the base type's polymorphic contract — and therefore its
discriminator — actually gets used; System.Text.Json only writes a `[JsonDerivedType]`
discriminator when serialization is driven by the declared base type).

## Polymorphic to-many/to-one relationships

A relationship's declared element type can be a polymorphic base class, letting a single
`List<T>`/`T[]` (or to-one property) hold instances of multiple different CLR subtypes:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Video), "videos")]
[JsonDerivedType(typeof(Image), "images")]
public abstract class Attachment
{
    [JsonApiId]
    public string Id { get; set; } = "";
}

[JsonApiResource("videos")]
public class Video : Attachment
{
    [JsonApiAttribute]
    public int DurationSeconds { get; set; }
}

[JsonApiResource("images")]
public class Image : Attachment { /* ... */ }

public class Article
{
    // ...
    [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
    public List<Attachment> Attachments { get; set; } = new();
}
```

- **Serialize**: already worked before this feature — each related object is resolved by its own
  runtime type (`relatedResource.GetType()`), so a `List<Attachment>` containing both `Video` and
  `Image` instances already emitted the correct `"type"` per element.
- **Deserialize**: this is the new part. Jsonapinator matches the incoming resource identifier's
  `"type"` string against the base type's `[JsonDerivedType]` registrations to pick the concrete
  CLR subtype to instantiate — **using System.Text.Json's own attributes as the source of truth,
  no separate Jsonapinator configuration needed.** This works even for an identifier-only stub (no
  matching entry in `"included"`) — the discriminator alone (already required by the JSON:API spec
  on every resource identifier object) is enough.
- **Constraint**: `[JsonDerivedType]` discriminators must be **strings**. JSON:API's own `"type"`
  member is always a string; System.Text.Json also allows `int` discriminators, but those have no
  meaningful counterpart on the wire and are ignored by Jsonapinator's relationship resolution.
- **Convention-mode note**: the JSON:API type name is derived from the camelCase class name (see
  [convention-based-mapping.md](convention-based-mapping.md)), so a convention-mapped subtype's
  class name must match its `[JsonDerivedType]` discriminator string exactly (e.g. a class named
  `Videos` for the discriminator `"videos"`) for round-tripping to work. Attribute-based mapping
  doesn't have this constraint since `[JsonApiResource("videos")]` sets the name explicitly,
  independent of the class name.
- An unrecognized discriminator (no matching `[JsonDerivedType]`) throws `JsonApiMappingException`.

See [`../samples/Jsonapinator.Sample.AttributeBased/Article.cs`](../samples/Jsonapinator.Sample.AttributeBased/Article.cs)'s
`MediaAsset`/`VideoAsset`/`ImageAsset` (and the equivalent `MediaAsset`/`Videos`/`Images` in
[`Jsonapinator.Sample.ConventionBased`](../samples/Jsonapinator.Sample.ConventionBased/Article.cs))
for the full runnable example — a different, narrower feature from the `Attachment`/`[JsonApiType]`
per-instance type override demonstrated alongside it (that lets *one* CLR type emit a *varying*
`"type"` name; this lets a relationship hold *multiple* CLR types).
