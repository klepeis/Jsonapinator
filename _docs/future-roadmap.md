# Jsonapinator — Future Roadmap

V1 covers the core JSON:API document structure only: primary data (single resource / resource
collection), attributes, relationships (to-one and to-many, as resource identifiers), links,
meta, and top-level errors. Everything below is explicitly out of scope for V1 and deferred to
later phases.

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
  integration package rather than the framework-agnostic core.
- **Atomic operations** (`POST /operations` with an array of operation objects) — a
  fundamentally different request/response shape from the rest of the spec. Strong candidate for
  a separate `Jsonapinator.Operations` package that depends on core `Jsonapinator` rather than
  growing the core library's surface area.

## Known V1 limitations (not roadmap items, just documented gaps)

- Relationships deserialize to identifier-only stub objects unless a matching resource is found
  in an incoming `"included"` array, in which case they're fully hydrated (see compound documents
  above). If the source document has no `"included"` member, stubs are always id-only, as before.
- To-many relationship properties must be `List<T>` or `T[]`; other collection types throw
  `JsonApiMappingException`.
- Resource ids are limited to `string`, `Guid`, `int`, and `long`.
