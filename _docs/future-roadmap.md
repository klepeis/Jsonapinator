# Jsonapinator — Future Roadmap

V1 covers the core JSON:API document structure only: primary data (single resource / resource
collection), attributes, relationships (to-one and to-many, as resource identifiers), document-,
resource-, and relationship-level links and meta (POCO-driven via `[JsonApiMeta]`/`[JsonApiLinks]`/
`[JsonApiRelationshipMeta]`/`[JsonApiRelationshipLinks]` or the `Meta`/`Links`/`{Rel}Meta`/
`{Rel}Links` naming convention — see `_docs/attribute-based-mapping.md` and
`_docs/convention-based-mapping.md`), a per-instance `"type"` override (`[JsonApiType]` or the
`Type` naming convention), polymorphic to-many/to-one relationships and polymorphic attribute
values (plain `System.Text.Json` `[JsonPolymorphic]`/`[JsonDerivedType]` — see
`_docs/polymorphism.md`), configurable depth/size limits on relationship hydration
(`JsonApiSerializerOptions`), and top-level errors. Everything below is explicitly out of scope
for V1 and deferred to later phases.

## Prioritized backlog

A read-through of every section below, ranked by risk/impact vs. effort. P0 is the "fix soon"
tier — real, concrete correctness/security gaps that are each individually cheap to address. P3 is
larger feature work with no forcing function yet.

| Pri | Item | Why this tier |
|---|---|---|
| **P0** | [`JsonApiDocumentReader` raw exceptions on malformed `"type"`/`"id"`/array elements](#security-considerations) | Directly on the untrusted-input deserialize path; throws framework exceptions instead of `JsonApiMappingException`, inconsistent with the library's own error contract. Moderate effort (wrap the existing null-forgiving/hard-cast sites). |
| **P0** | [Shape-mismatched body → 500, not 400](#jsonapinatoraspnetcore--deferred-concerns) (`Deserialize`/`DeserializeCollection`, all 4 call sites) | High likelihood — any client sending an array where a single resource is expected (or vice versa) hits this. Moderate effort, all 4 sites share the same fix shape. |
| **P2** | [`WalkIncludes` linear relationship-by-name scan](#performance-considerations) / [`IsRelationshipTarget` redundant reflection](#performance-considerations) | Real but only matters at scale (large included graphs / wide object models). Moderate effort — index `ResourceMetadata.Relationships` by name; share the classify/build reflection pass. |
| **P2** | [`JsonApiInputFormatter` fully buffers the request body](#security-considerations) (no streaming) | Real perf/memory concern under load, but a genuine rewrite (stream-based parsing) rather than a small patch — bigger effort than the P0 size-cap mitigation, which is the cheaper interim fix for the same risk. |
| **P2** | [Cycle-truncated relationships are an undocumented contract](#correctnessrobustness-gaps) | Docs-only fix — add one sentence to `compound-documents.md` about the id-only-stub fallback on cycles. Trivial effort, do it opportunistically. |
| **P2** | [`ToAttributePointer` misleading for dotted/prefixed `ModelState` keys](#correctnessrobustness-gaps) | DX-only (still a valid 400 either way); moderate effort to handle every binder key shape well, low urgency. |
| **P3** | [`Include` not wired through `JsonApiOutputFormatter`](#jsonapinatoraspnetcore--deferred-concerns) / [PATCH-style partial update not wired through `JsonApiInputFormatter`](#jsonapinatoraspnetcore--deferred-concerns) | Real feature gaps with a documented workaround (the DI escape hatch) already in place via the samples — larger, deliberate design work, not urgent. |
| **P3** | [Strict 415/406 content-negotiation parameter rejection](#phase-3--extensions-and-profiles) | Spec-compliance nice-to-have; no one has hit this in practice yet. |
| **P3** | Sparse fieldsets, sorting, pagination, filtering ([Phase 2](#phase-2--query-string-driven-features)) | Larger, deliberate feature work — each already has a documented seam, build when there's a concrete consumer need. |
| **P3** | [Client-generated ids](#additional-feature-gaps-not-yet-in-phase-23-above), [omit-relationship-from-output](#additional-feature-gaps-not-yet-in-phase-23-above) | Smaller, self-contained feature gaps — pick up opportunistically alongside related work (e.g. omit-relationship pairs naturally with sparse fieldsets). |
| **P3** | [Composite/fallback resolver](#known-extension-points), [convention-resolver ambiguity/cycle detection](#additional-feature-gaps-not-yet-in-phase-23-above) | Extension points with no current consumer need — cheap to build later, not worth speculative effort now. |
| **P3** | Atomic operations, extensions/profiles negotiation ([Phase 3](#phase-3--extensions-and-profiles)) | Largest scope items, explicitly flagged as candidates for separate packages rather than growing the core. |

The sections below retain the full detail behind each item.

## Phase 2 — Query-string-driven features

These features are all about *shaping* a response based on request query parameters. None of
them are needed to produce a spec-compliant document, which is why they're deferred — but each
has a natural seam in the current architecture:

- **Sparse fieldsets** (`fields[type]=title,body`) — restrict which attributes/relationships are
  included in the output. Seam: `ResourceGraphBuilder.BuildResource` would accept an optional
  per-type field allowlist (likely via `JsonApiDocumentOptions`) and skip metadata entries not in
  the allowlist.
- ~~**Compound documents / `include`**~~ — **done.** `JsonApiDocumentOptions.Include` accepts
  dot-notation relationship paths (e.g. `["author", "comments.author"]`); `ResourceGraphBuilder`
  walks the requested paths against the real, already-loaded relationship objects and populates
  `JsonApiDocument.Included`, and `ResourceMapper` hydrates relationship objects from an incoming
  `"included"` array instead of leaving them as id-only stubs. The relationship-loader
  abstraction originally guessed at here turned out to be unnecessary — relationship properties
  were already fully-loaded real objects in memory the whole time; a query-string `include=...`
  parsing helper is still not provided (consumers turn `?include=` into the path list themselves).
- **Sorting** (`sort=-created,title`) — purely a request-side concern; Jsonapinator would only
  need a small query-string parser producing an ordered list of (field, direction) pairs. Actual
  sorting stays the consumer's responsibility (e.g. in their data access layer).
- **Pagination** (page-based `page[number]`/`page[size]` and cursor-based) — primarily affects
  the top-level `links` (`first`/`last`/`prev`/`next`) and `meta` (`total`) members. Seam:
  extend `JsonApiDocumentOptions` with pagination link/meta builders.
- **Filtering** — the spec deliberately leaves filtering conventions unspecified. Jsonapinator
  would provide a query-string parser for a common convention (e.g. `filter[title]=foo`) but
  leave applying the filter to the consumer, same as sorting.

## Phase 3 — Extensions and profiles

- **JSON:API extensions/profiles negotiation** (`Content-Type` media type parameters like
  `ext="..."`) — content-negotiation concerns that likely belong in an ASP.NET Core-specific
  integration package rather than the framework-agnostic core. ~~This now has a concrete home~~:
  `Jsonapinator.AspNetCore` exists (see below); strict enforcement of the spec's 415/406
  parameter-rejection rule is still not built there.
- **Atomic operations** (`POST /operations` with an array of operation objects) — a
  fundamentally different request/response shape from the rest of the spec. Strong candidate for
  a separate `Jsonapinator.Operations` package that depends on core `Jsonapinator` rather than
  growing the core library's surface area.

## `Jsonapinator.AspNetCore` — deferred concerns

`Jsonapinator.AspNetCore` (custom MVC input/output formatters for `application/vnd.api+json`,
see [`aspnetcore-integration.md`](aspnetcore-integration.md)) intentionally stays scoped to
format conversion. Deferred from that pass, not yet built:

- ~~**Validation-failure and unhandled-exception → JSON:API error document mapping.**~~ — **done.**
  `AddJsonApi()` replaces `ApiBehaviorOptions.InvalidModelStateResponseFactory` and registers
  `JsonApiExceptionHandler` (an `IExceptionHandler`) to map invalid `ModelState` (400) and
  unhandled exceptions (500, generic detail only — never the real exception message) to JSON:API
  error documents. Negotiation-aware by default (`Accept: application/vnd.api+json` required, else
  ASP.NET Core's normal `ProblemDetails` is preserved); `options.MapErrorsAlways()` forces it
  regardless. Unhandled-exception mapping additionally requires `app.UseExceptionHandler()` in
  `Program.cs` — registering the DI service alone doesn't wire it into the pipeline. See
  [`aspnetcore-integration.md`](aspnetcore-integration.md#error-documents).
- **Strict content-negotiation parameter rejection.** The JSON:API spec requires rejecting
  `Content-Type`/`Accept` values that include media-type parameters (415/406) — `TextInputFormatter`/
  `TextOutputFormatter` don't give this for free; it would need `CanWriteResult` overrides and/or
  manual `Accept`-header parameter inspection.
- **Shape-mismatched body → 500, not 400.** `JsonApiSerializer.Deserialize`, `Deserialize<T>`,
  `DeserializeCollection<T>`, and `DeserializeCollection(Type, ...)` all dereference
  `document.Data!.Single!`/`document.Data!.Collection!` without checking — a JSON:API array
  posted to an action expecting a single resource (or vice versa) throws a raw null-reference
  (surfacing as an unhandled 500 through `JsonApiInputFormatter`) instead of
  `JsonApiMappingException`/400. This is a pre-existing gap in core `Jsonapinator`'s deserialize
  path (all four call sites), not specific to the ASP.NET Core integration.
- **`Include`/`JsonApiDocumentOptions` isn't wired through `JsonApiOutputFormatter`.** A
  controller action returning a POCO cannot request compound documents automatically — the only
  way today is the `JsonApiSerializer` DI escape hatch (see the `with-includes` action in
  `samples/Jsonapinator.Sample.ConventionBased`). Natural seam: read `?include=` from the request
  and pass it through in the formatter, or a dedicated action filter/attribute.
- **PATCH-style partial-update semantics aren't wired through `JsonApiInputFormatter`.** The
  formatter always constructs a fresh instance (`ResourceMapper.Map`/`CreateInstance`); true
  presence-based partial updates onto an already-loaded entity require calling
  `ResourceMapper.MapOnto` manually via the escape hatch, which isn't documented today.
- ~~**No idempotency guard if `AddJsonApi()` is called twice**~~ — **done.** A second call on the
  same `IMvcBuilder` is now a no-op (checked via whether `JsonApiFormatterOptions` is already
  registered) instead of double-registering the input/output formatters, the
  `JsonApiSerializer`/`JsonApiFormatterOptions` DI singletons, the exception handler, and wrapping
  `InvalidModelStateResponseFactory` in a second layer of delegation.

## Security considerations

Findings from a broad code review (2026-07), documentation only — no code changes made yet:

- ~~**No recursion/depth limit on `included`-driven relationship hydration.**~~ — **done.**
  `JsonApiSerializerOptions.MaxIncludeDepth` (default 32) bounds relationship-hydration recursion
  in both `ResourceMapper.BuildRelatedInstance` (deserialize) and
  `ResourceGraphBuilder.WalkIncludes` (serialize, defense-in-depth for when `Include` paths are
  eventually wired to request input) — a chain deeper than the configured limit throws
  `JsonApiMappingException` instead of risking an uncatchable `StackOverflowException`. Genuine
  cycles are still guarded separately by the existing `visiting` set.
- ~~**No size cap on `included` arrays or to-many relationship arrays during deserialize.**~~ —
  **done.** `JsonApiSerializerOptions.MaxIncludedResources` and `.MaxToManyRelationshipSize`
  (both default 5000) cap the `"included"` array size (`ResourceMapper.BuildLookup`) and each
  to-many relationship's `"data"` array size (`ResourceMapper.BuildRelationshipValue`) — both
  throw `JsonApiMappingException` before allocating further when exceeded. All three limits are
  configurable per `JsonApiSerializer` (constructor parameter) and, for `Jsonapinator.AspNetCore`,
  via `JsonApiFormatterOptions.WithMaxIncludeDepth`/`.WithMaxIncludedResources`/
  `.WithMaxToManyRelationshipSize`.
- **`JsonApiInputFormatter.ReadRequestBodyAsync` fully buffers the request body into a `string`**
  (and `JsonApiDocumentReader` then builds a second full in-memory `JsonNode` DOM) rather than
  streaming — a memory/CPU amplification vector for large bodies, unlike ASP.NET Core's own
  `SystemTextJsonInputFormatter`, which parses incrementally from the request stream.
- **`JsonApiDocumentReader` has several null-forgiving (`!`)/hard-cast dereferences on
  attacker-controlled JSON** — a resource/identifier object missing `"type"`, `"type"` present but
  not a string, or a non-object element inside `"included"`/`"errors"`/a relationship's `"data"`
  array all throw raw `NullReferenceException`/`InvalidCastException`/`InvalidOperationException`
  instead of `JsonApiMappingException`. Inconsistent with the library's own error contract, not
  just a correctness nit, since it's directly on the deserialize entry point for untrusted input.
- ~~**No idempotency guard against calling `AddJsonApi()` twice**~~ — **done** (see the ASP.NET
  Core deferred concerns above) — closes the edge case where double-registered exception handlers
  could attempt to write to an already-started response.
- **Positive finding, worth preserving (updated)**: reflection in both `ResourceTypeResolver` and
  `ConventionResourceTypeResolver` only ever operates on CLR types supplied by the consumer's own
  code — never an arbitrary type name taken from the wire (`ResourceMapper.Map`/`Map(Type, ...)`
  always take the target CLR type from the caller). Polymorphic relationships (see
  `_docs/polymorphism.md`) do let a wire value (a resource identifier's `"type"` string)
  participate in selecting a CLR type for the first time, but only in a closed, safe form: the
  result is always one of the finite `[JsonDerivedType]`-registered subtypes the consumer
  explicitly allow-listed at compile time on the relationship's own declared base type — never
  arbitrary reflection/type-name resolution. The library is still not exposed to the classic
  "polymorphic type resolution from JSON" insecure-deserialization pattern (unbounded/attacker-
  chosen type names), just to a bounded, compile-time-declared subset of it.

## Performance considerations

- **`ConventionResourceTypeResolver.IsRelationshipTarget` re-reflects a candidate type's
  properties purely to classify it**, redundant with the full `Build` reflection pass that runs if
  that same type is later `Resolve()`d directly — no cache reuse between the two call sites.
- **`ResourceGraphBuilder.WalkIncludes` resolves each include-path segment via a linear
  `FirstOrDefault` scan** over `ResourceMetadata.Relationships` (a `List<T>`) rather than a
  name-indexed lookup — O(segments × relationships) repeated per node on large included graphs.
- **`PropertyInfo.GetValue`/`SetValue` reflection with no compiled-delegate/expression-tree
  caching** in `ResourceGraphBuilder.BuildResource` and `ResourceMapper.MapOnto` — fine at small
  scale, a real cost for large collection serialize/deserialize.
- **Extra allocations on the ASP.NET Core hot path**: `JsonApiSerializer.DeserializeCollection(Type, ...)`
  does `Activator.CreateInstance(typeof(List<>).MakeGenericType(...))` per call, and
  `JsonApiInputFormatter.AdaptCollection` does a full `List<T>`→`T[]` copy for every array-typed
  `[FromBody]` action parameter.
- General LINQ-allocation overhead throughout `JsonApiDocumentReader`'s parse path
  (`.Select().ToList()`/`.ToDictionary()`) versus plain loops — minor but pervasive.
- Confirmed **not** an issue: `JsonApiSerializer`/resolvers are constructed once at `AddJsonApi()`
  startup and shared as DI singletons across the formatters — no per-request construction.

## Known extension points

- **Composite/fallback resolver**: `JsonApiSerializer` now ships two `IResourceTypeResolver`
  implementations — attribute-based (`ResourceTypeResolver`, the default) and convention-based
  (`ConventionResourceTypeResolver`, via `JsonApiSerializer.WithConventions()`). Since both share
  the same `Resolve(Type) : ResourceMetadata` contract, a composite resolver that tries one and
  falls back to the other per-type (e.g. "use attributes if present, otherwise fall back to
  convention") would be a cheap addition later if a mixed-mode use case comes up — not built now
  since nothing has asked for it yet.

## Correctness/robustness gaps

- ~~`ResourceTypeResolver`'s `properties.SingleOrDefault(p => p.IsDefined(typeof(JsonApiIdAttribute)))`
  throws a generic LINQ `InvalidOperationException`~~ — **done.** Replaced with an explicit
  `.Where(...).ToList()` + `Count > 1` check (matching the pattern already used by
  `FindSingleTypedProperty`/`FindRelationshipTypedProperty` in the same file), throwing
  `JsonApiMappingException` when a type mistakenly has two `[JsonApiId]` properties.
- ~~No wrapping around reflection `GetValue`/`SetValue` calls or `JsonNode.Deserialize`
  type-mismatch failures~~ — **done.** `ResourceMapper.ConvertAttributeValue` now wraps
  `node.Deserialize(...)` in a try/catch, rethrowing `JsonException` as `JsonApiMappingException`
  naming the offending attribute — a client sending a wrong-typed attribute value (e.g. a JSON
  string where an `int` is expected) now gets the library's own error contract instead of a raw
  `JsonException`.
- `JsonApiInvalidModelStateResponseFactory.ToAttributePointer` produces misleading pointers (not
  just "imprecise," and not only for the already-documented indexed-key case, e.g.
  `"Comments[0].Body"`) for dotted nested-binding keys (`"Author.Name"` → `/data/attributes/author.name`,
  not a real attribute path) and binder-prefixed keys (`"$.Title"`/`"model.Title"`).
- ~~**Nested object/array attribute values aren't camelCased.**~~ — **done.**
  `ResourceGraphBuilder.BuildAttributeValue`, `JsonApiDocumentWriter.WriteResource`'s attribute
  loop, and `ResourceMapper.ConvertAttributeValue` now all use the shared
  `NestedValueSerialization.CamelCase` (`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`,
  `PropertyNameCaseInsensitive = true`) instead of default options — a nested object attribute's
  own properties (e.g. `ArticleSeo { MetaTitle, MetaDescription }`) now serialize/deserialize
  camelCased, consistent with the attribute's own key. Case-insensitive read means documents
  already written with the old PascalCase nested keys still deserialize correctly. Also applies to
  the polymorphic single-valued/collection-valued attribute paths (see `_docs/polymorphism.md`)
  for the same consistency.
- Cycle-truncated relationships in compound-document hydration silently fall back to id-only stubs
  with no signal to the consumer that hydration was cut short — a reasonable design choice, but
  currently undocumented as an explicit contract.

## Additional feature gaps (not yet in Phase 2/3 above)

- No way to omit a relationship from output entirely (as opposed to serializing `data: null`) —
  adjacent to, but distinct from, the already-deferred sparse-fieldsets item.
- No client-generated-id support (the spec's client-ID section).
- ~~No polymorphic/heterogeneous to-many relationship support~~ — **done.** A relationship's
  declared `RelatedClrType` can now be a polymorphic base class (plain `System.Text.Json`
  `[JsonPolymorphic]`/`[JsonDerivedType]` attributes, no Jsonapinator-specific vocabulary);
  `RelationshipMetadata.PolymorphicDerivedTypes` maps each JSON:API type-name discriminator to its
  CLR subtype, resolved lazily in `ResourceMapper.BuildRelatedInstance`. Distinct from
  `[JsonApiType]`/the `Type` naming convention (see `_docs/attribute-based-mapping.md`/
  `convention-based-mapping.md`), which only lets a *single* CLR type emit a varying JSON:API
  `"type"` name per instance — see `_docs/polymorphism.md` for both features side by side.
- No ambiguity/cycle detection at metadata-build time for `ConventionResourceTypeResolver` on
  self-referential or mutually-referential convention types — could silently misclassify in
  unusual shapes.

## Known V1 limitations (not roadmap items, just documented gaps)

- Relationships deserialize to identifier-only stub objects unless a matching resource is found
  in an incoming `"included"` array, in which case they're fully hydrated (see compound documents
  above). If the source document has no `"included"` member, stubs are always id-only, as before.
- To-many relationship properties must be `List<T>` or `T[]`; other collection types throw
  `JsonApiMappingException`.
- Resource ids are limited to `string`, `Guid`, `int`, and `long`.
