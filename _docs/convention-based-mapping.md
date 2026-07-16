# Convention-based mapping

An alternative to [attribute-based mapping](attribute-based-mapping.md): map plain POCOs with
zero `Jsonapinator.Attributes`, by calling `JsonApiSerializer.WithConventions()` instead of
`new JsonApiSerializer()`. Every element of the POCO is assumed to belong in the JSON:API
document unless it's explicitly unmappable (an indexer, or a get-only/set-only property).

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
Article roundTripped = serializer.Deserialize<Article>(json);
```

See [`../samples/Jsonapinator.Sample.ConventionBased`](../samples/Jsonapinator.Sample.ConventionBased)
for a runnable ASP.NET Core example covering relationships, nested object/array attributes, and a
`Guid`-keyed resource.

## When to use this over attribute-based mapping

- Your POCOs already follow ordinary .NET conventions (a property named `Id`, navigation
  properties pointing at other id-bearing types) and you don't want to decorate every one of
  them just to serialize them.
- You're fine with the resource type name being the camelCase class name — see the naming rule
  below. If you need a specific type name (e.g. plural `"people"` instead of `"person"`), use
  [attribute-based mapping](attribute-based-mapping.md) with `[JsonApiResource("people")]` instead.

Convention mode and attribute mode are mutually exclusive per `JsonApiSerializer` instance — a
type is resolved entirely by whichever resolver that instance was built with (there's no mixing,
and no attribute on a POCO changes convention-mode behavior).

## The classification rule

Applied to every public property with both a getter and a setter. Indexers and get-only/
set-only properties are silently skipped — never mapped, never an error:

1. **Id**: a property literally named `Id` (of type `string`, `Guid`, `int`, or `long`) becomes
   the resource id. Exactly one is required, or `JsonApiSerializer` throws
   `JsonApiMappingException` when the type is first resolved.
2. **Resource type name**: the camelCase class name, **not pluralized** —
   `Article` → `"article"`, `OrderLine` → `"orderLine"`. English pluralization is unreliable
   (`Person` → `"People"`, not `"Persons"`), so this mode deliberately doesn't attempt it.
3. **Relationship**: a property whose type (or element type, for `List<T>`/`T[]`) is itself a
   class with its own usable `Id` property becomes a relationship — to-one for a single
   reference, to-many for a collection.
4. **Attribute**: everything else — primitives, `string`, `Guid`, `DateTime`, `decimal`, enums,
   and nested objects/collections whose element type has no `Id` property — becomes a flat
   attribute, serialized as-is (nested objects and arrays included).

Rule 3 needs no explicit list of "scalar" BCL types to exclude (`Guid`, `DateTime`, `decimal`,
`Uri`, ...): none of them have a public `Id` property, so they fall through to rule 4
automatically. `[JsonPropertyName]` overrides are still respected for attribute names, same as
attribute-based mapping.

### Worked example

```csharp
public class Article
{
    public string Id { get; set; } = "";              // -> id
    public string Title { get; set; } = "";            // -> attribute "title"
    public DateTime PublishedAtUtc { get; set; }        // -> attribute "publishedAtUtc" (DateTime has no Id property)
    public Person? Author { get; set; }                 // -> to-one relationship "author" (Person has an Id property)
    public List<Comment> Comments { get; set; } = new(); // -> to-many relationship "comments" (Comment has an Id property)
    public Address? ShippingAddress { get; set; }        // -> attribute "shippingAddress" (Address has no Id property)
}
```

`ResourceType = "article"`, `IdProperty = Id`, `Attributes = [Title, PublishedAtUtc, ShippingAddress]`,
`Relationships = [Author (ToOne -> Person), Comments (ToMany -> Comment)]`.

For fetching related resources' own attributes into the response (not just their id/type), see
[compound-documents.md](compound-documents.md) — it works identically regardless of which
mapping mode built the `JsonApiSerializer`.

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
[`../samples/Jsonapinator.Sample.ConventionBased/ArticlesController.cs`](../samples/Jsonapinator.Sample.ConventionBased/ArticlesController.cs)'s
`with-document-meta` action for a runnable example.

## Resource-level and relationship-level `meta`/`links`

Unlike document-level `meta`/`links`, these live directly on the POCO and need no explicit
declaration — they're recognized by property name and type, same spirit as the `Id` rule above:

5. **Resource-level meta/links**: a property named exactly `Meta` of type exactly `MetaObject`
   becomes the resource object's own `"meta"`; a property named exactly `Links` of type exactly
   `LinksObject` becomes its own `"links"`.
6. **Relationship-level meta/links**: for a relationship property named e.g. `Comments`, sibling
   properties named `CommentsMeta` (type `MetaObject`) and `CommentsLinks` (type `LinksObject`)
   become that relationship object's own `"meta"`/`"links"` — one meta/links object per
   relationship as a whole (matching the JSON:API relationship-object shape), not per related
   resource.

```csharp
public class Article
{
    public string Id { get; set; } = "";
    public List<Comment> Comments { get; set; } = new();

    public MetaObject? Meta { get; set; }         // -> the resource object's own "meta"
    public LinksObject? Links { get; set; }       // -> the resource object's own "links"

    public MetaObject? CommentsMeta { get; set; }   // -> the "comments" relationship's own "meta"
    public LinksObject? CommentsLinks { get; set; } // -> the "comments" relationship's own "links"
}
```

**This is deliberately more lenient than attribute-based mapping**: if a property happens to be
named `Meta`/`Links`/`{RelationshipName}Meta`/`{RelationshipName}Links` but isn't of the exact
required type, it falls through to normal classification (a flat attribute, or a relationship) —
convention mode never throws for this, unlike `[JsonApiMeta]`/`[JsonApiLinks]` in attribute mode,
to avoid breaking an existing type that happens to have an unrelated same-named property. If you
want stricter enforcement, use [attribute-based mapping](attribute-based-mapping.md) instead.

On deserialize, mapping is presence-based like attributes and relationships: if the incoming JSON
omits a resource's/relationship's `meta`/`links`, the corresponding POCO property is left
untouched (so a PATCH-style partial payload can't clobber it).

See
[`../samples/Jsonapinator.Sample.ConventionBased/Article.cs`](../samples/Jsonapinator.Sample.ConventionBased/Article.cs)
for the full runnable example.

## Per-instance `"type"` override

Rule 2 above derives the resource type name from the camelCase class name — one fixed name for
every instance of the class. A property named exactly `Type` of type exactly `string` overrides
that name on a per-instance basis instead — useful for a discriminator-style CLR type shared by
several JSON:API resource types (e.g. one `Attachment` class that emits `"videos"` for some
instances and `"images"` for others, based on data rather than CLR type):

```csharp
public class Attachment
{
    public string Id { get; set; } = "";
    public string? Type { get; set; } // null/empty -> falls back to "attachment"
}
```

Same leniency rule as `Meta`/`Links` above: a `Type`-named property of the wrong CLR type falls
through to being an ordinary flat attribute rather than being treated as the override (this is
a new reserved property name, alongside `Id`/`Meta`/`Links` — an existing convention-based POCO
with an unrelated `string Type` property will see it become the type override instead of a flat
`attributes.type` once this rule applies; rename the property, or switch to
[attribute-based mapping](attribute-based-mapping.md)'s `[JsonApiType]`, if that's not wanted).
On deserialize, the property is populated from the incoming resource's actual `"type"` string,
including on identifier-only relationship stubs.

See
[`../samples/Jsonapinator.Sample.ConventionBased/Article.cs`](../samples/Jsonapinator.Sample.ConventionBased/Article.cs)'s
`Attachment` class for the full runnable example.
