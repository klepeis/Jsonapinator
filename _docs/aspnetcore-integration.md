# ASP.NET Core integration

`Jsonapinator.AspNetCore` makes `application/vnd.api+json` work automatically for ASP.NET Core
MVC Web API projects — controller actions return/accept plain POCOs and the framework handles
JSON:API serialization based on `Accept`/`Content-Type` headers, via ASP.NET Core's normal
content-negotiation pipeline. No per-action code, no attributes beyond the ones you already use
for [attribute-based mapping](attribute-based-mapping.md) (or none at all, with
[convention-based mapping](convention-based-mapping.md)).

The core `Jsonapinator` project stays dependency-free — this is a separate sibling project that
references it and additionally depends on `Microsoft.AspNetCore.App` (via `FrameworkReference`,
not a NuGet package).

## Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonApi();

var app = builder.Build();
app.UseExceptionHandler(); // required for unhandled exceptions to map to JSON:API errors — see "Error documents" below
app.MapControllers();
app.Run();
```

That's it — any controller action can now return or accept a plain POCO, and a request with
`Accept: application/vnd.api+json` gets a JSON:API-shaped response with
`Content-Type: application/vnd.api+json`; a request with `Content-Type: application/vnd.api+json`
gets its body deserialized into the action's parameter type.

`AddJsonApi()` with no configuration maps POCOs by **convention** (no `Jsonapinator.Attributes`
required — see [convention-based-mapping.md](convention-based-mapping.md) for the classification
rule: a property named `Id` becomes the resource id, id-bearing property types become
relationships, everything else becomes an attribute):

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

public class Article
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}
```

## Attribute-based mapping

To map POCOs via explicit [`Jsonapinator.Attributes`](attribute-based-mapping.md) instead (e.g.
for a specific resource type name, or explicit control over which properties are exposed):

```csharp
builder.Services.AddControllers().AddJsonApi(options => options.UseAttributes());
```

## How it works

`AddJsonApi()` registers a single shared `JsonApiSerializer` (convention-based by default, or
attribute-based per `UseAttributes()`) as both a DI singleton (so your own code can inject it
for the `BuildDocument`/`ParseDocument` escape hatches) and inside two MVC formatters, inserted
ahead of the default System.Text.Json formatters:

- **`JsonApiOutputFormatter`** — a `TextOutputFormatter` that writes a controller action's return
  value as a JSON:API document (single resource, or a collection document when the value is an
  `IEnumerable`).
- **`JsonApiInputFormatter`** — a `TextInputFormatter` that reads a JSON:API request body into the
  action parameter's type (single resource, or a collection for `List<T>`/`T[]`/`IEnumerable<T>`
  parameters).

Both only ever activate when `application/vnd.api+json` is the negotiated media type — they don't
affect any endpoint that doesn't use it.

**Known gap**: `JsonApiOutputFormatter` always calls
`JsonApiSerializer.Serialize(context.Object!)` with no `JsonApiDocumentOptions`, so a controller
action returning a POCO cannot request compound documents (`Include`) automatically — there's no
`?include=` wiring today. The workaround is the `JsonApiSerializer` DI escape hatch: inject it,
call `Serialize(resource, new JsonApiDocumentOptions { Include = [...] })` yourself, and return
`Content(json, JsonApiOutputFormatter.MediaType)` — see the `with-includes` action in
`samples/Jsonapinator.Sample.ConventionBased`. Same gap applies to PATCH-style partial updates on
the input side: the formatter always builds a fresh instance rather than mapping onto an
already-loaded entity; true partial updates need `ResourceMapper.MapOnto` called manually. See
`future-roadmap.md`.

## Malformed request bodies → 400

If a request body can't be mapped (malformed JSON, an id whose value doesn't parse as the
declared id type, etc.), `JsonApiInputFormatter` catches the resulting
`Jsonapinator.Exceptions.JsonApiMappingException`, adds a model state error, and returns
`InputFormatterResult.Failure()` — the same convention ASP.NET Core's own JSON input formatter
uses, so `[ApiController]`'s automatic invalid-model-state behavior produces a `400 Bad Request`
with zero extra wiring. Whether the *body* of that 400 is a JSON:API errors document or ASP.NET
Core's default `ProblemDetails` is covered next.

**Known gap**: a body that's valid JSON but the wrong *shape* for the action (e.g. a JSON:API
array posted to an action expecting a single resource) currently surfaces as an unhandled 500
rather than a 400 — `JsonApiSerializer.Deserialize`/`Deserialize<T>` assume `document.Data` is a
single resource without checking, in core `Jsonapinator`. See `future-roadmap.md`.

## Error documents

`AddJsonApi()` automatically maps two error conditions to JSON:API `{"errors":[...]}` documents
(`ErrorObject`/`SerializeErrors`):

- **Invalid `ModelState`** (e.g. the malformed-body case above) — `400`, one `ErrorObject` per
  individual validation error, `Source.Pointer` best-effort derived from the `ModelState` key
  (e.g. key `"Title"` → `/data/attributes/title`; an empty key, the common case for whole-body
  `[FromBody]` binding, → `/data`). This is a best-effort convention — it won't produce a valid
  pointer for nested/indexed keys (e.g. `"Comments[0].Body"`).
- **Unhandled exceptions** — `500`, a single generic `ErrorObject` (`"An unexpected error
  occurred."`). The real exception message/stack trace is **never** included in the response
  body, regardless of environment — it's still logged in full server-side via `ILogger`.

**By default, both are negotiation-aware**: they only produce a JSON:API error document when the
client's `Accept` header actually included `application/vnd.api+json`; otherwise ASP.NET Core's
normal `ProblemDetails` response is preserved untouched. Force JSON:API errors regardless of what
was negotiated with:

```csharp
builder.Services.AddControllers().AddJsonApi(options => options.MapErrorsAlways());
```

See `samples/Jsonapinator.Sample.ErrorHandling.Default` and
`samples/Jsonapinator.Sample.ErrorHandling.AlwaysMap` for the two configurations side by side,
with `curl` commands showing the behavioral difference.

**Unhandled-exception mapping requires one extra step**: `AddJsonApi()` can only register
`JsonApiExceptionHandler` as a DI service (`IExceptionHandler`) — it cannot wire it into the
application pipeline, since that requires `IApplicationBuilder`, only available after
`builder.Build()`. You must call `app.UseExceptionHandler();` yourself (as shown in Setup above);
without it, `JsonApiExceptionHandler` is registered but never runs. Invalid-`ModelState` mapping
does **not** need this — it's wired through `ApiBehaviorOptions.InvalidModelStateResponseFactory`,
which `AddJsonApi()` can configure directly.

## Known limitations

- The JSON:API spec's requirement to reject `Content-Type`/`Accept` values that include media-type
  parameters (415/406) isn't enforced — the bare `application/vnd.api+json` media type is
  registered and matched by ASP.NET Core's normal content negotiation. See `future-roadmap.md`.

## Samples

- `samples/Jsonapinator.Sample.ConventionBased` — relationships, nested object/array attributes, a
  `Guid`-keyed resource, and the `Include` escape hatch, all mapped by convention.
- `samples/Jsonapinator.Sample.AttributeBased` — the same resource graph mapped via explicit
  `Jsonapinator.Attributes`, including a `[JsonPropertyName]` override.
- `samples/Jsonapinator.Sample.ErrorHandling.Default` and
  `samples/Jsonapinator.Sample.ErrorHandling.AlwaysMap` — the two error-mapping configurations
  side by side, each with `curl` walkthroughs in its `Program.cs` header comment.

```
dotnet run --project samples/Jsonapinator.Sample.ConventionBased
curl -H "Accept: application/vnd.api+json" http://localhost:5289/articles
```
