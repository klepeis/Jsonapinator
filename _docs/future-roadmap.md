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
- **Compound documents / `include`** (`include=author,comments.author`) — populate the top-level
  `included` array with related resources, not just their identifiers. This is the biggest
  addition: it requires a relationship-loader abstraction (something that can fetch the actual
  related object given a stub/identifier), since `ResourceGraphBuilder` currently only reads
  properties already present on the POCO in memory. Likely a new `IRelatedResourceLoader`
  interface, resolved per relationship.
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

- Relationships deserialize to identifier-only stub objects (id + type set, nothing else) since
  compound documents/`include` aren't supported yet — see Phase 2 above.
- To-many relationship properties must be `List<T>` or `T[]`; other collection types throw
  `JsonApiMappingException`.
- Resource ids are limited to `string`, `Guid`, `int`, and `long`.
