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
