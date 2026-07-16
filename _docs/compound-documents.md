# Compound documents (`include`)

Populate a JSON:API document's top-level `"included"` array so related resources' own attributes
travel with the response, instead of the response only carrying `{type, id}` for each
relationship. Works identically under [attribute-based](attribute-based-mapping.md) and
[convention-based](convention-based-mapping.md) mapping.

## Serialize

Pass dot-notation relationship paths via `JsonApiDocumentOptions.Include`:

```csharp
var options = new JsonApiDocumentOptions { Include = new[] { "author", "comments.author" } };
string json = serializer.Serialize(article, options);
```

Related objects are read via plain reflection (`PropertyInfo.GetValue`) directly off the POCO's
relationship properties. Jsonapinator has no dependency on Entity Framework Core or any other
ORM/database library (the core project has zero `PackageReference`s beyond the .NET 8 BCL) and
does not know or care where the object graph came from — it simply assumes relationship
properties are already populated in memory by the time `Serialize` is called (e.g. via EF Core
`.Include()`, a manual query, an in-memory fixture, etc.). It does not lazy-load anything itself.

## Deserialize

When the source JSON has an `"included"` array, matching relationships are fully hydrated
(attributes and their own nested relationships populated) instead of being left as id-only
stubs — this happens automatically, no extra API call needed:

```csharp
Article article = serializer.Deserialize<Article>(json);
article.Author!.FirstName // populated if "author" was present in "included"
```

If the source JSON has no `"included"` member, or no entry matching a given relationship,
that relationship still deserializes to an id-only stub — exactly as it does without this
feature at all.

## Limitations

- No query-string `?include=...` parsing helper is provided — consumers turn that into an
  `IEnumerable<string>` of paths themselves.
- A resource never appears twice across primary data + `included` (deduplicated by
  `{type, id}`), and a resource that's part of the primary data is never also duplicated into
  `included`.
- **In ASP.NET Core, `Include` isn't wired through the automatic output formatter** —
  `JsonApiOutputFormatter` always calls `Serialize`/`SerializeCollection` with no
  `JsonApiDocumentOptions`, so a controller action returning a POCO can't request compound
  documents automatically. The current workaround is to inject `JsonApiSerializer` (registered as
  a DI singleton by `AddJsonApi()`) and call `Serialize(resource, options)` directly — see the
  `with-includes` action in
  [`../samples/Jsonapinator.Sample.ConventionBased`](../samples/Jsonapinator.Sample.ConventionBased).
  See `future-roadmap.md`.
