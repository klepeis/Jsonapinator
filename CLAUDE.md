# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Jsonapinator: a .NET 8 library implementing the [JSON:API](https://jsonapi.org/format/) spec's
document structure (serialize/deserialize), plus a separate ASP.NET Core integration package that
makes `application/vnd.api+json` work automatically for Web API controllers. Built test-first
(TDD) following SOLID principles — every feature in this repo was added via a failing-test-first
cycle; keep doing that for new work.

## Commands

```bash
dotnet build                                    # whole solution
dotnet test                                     # whole solution (two test projects)
dotnet test test/Jsonapinator.Tests/Jsonapinator.Tests.csproj              # core lib only
dotnet test test/Jsonapinator.AspNetCore.Tests/Jsonapinator.AspNetCore.Tests.csproj  # ASP.NET Core only

# Run a single test
dotnet test --filter "FullyQualifiedName~ResourceTypeResolverTests.Resolve_throws_when_no_Id_property_exists"
dotnet test --filter "DisplayName~SomeTestName"

# Run a sample (each has curl commands in its Program.cs header comment)
dotnet run --project samples/Jsonapinator.Sample.ConventionBased
```

All `.csproj` files set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — a warning fails
the build. There is no separate lint step; `dotnet build` is the check.

## Solution layout

- `src/Jsonapinator/` — the core library. **Zero `PackageReference`s** beyond the .NET 8 BCL
  (`Microsoft.NET.Sdk`, System.Text.Json only) — this is deliberate; keep it dependency-free.
- `src/Jsonapinator.AspNetCore/` — ASP.NET Core MVC integration. References core `Jsonapinator`
  plus `Microsoft.AspNetCore.App` via `<FrameworkReference>` (not a NuGet package) — the standard
  pattern for an ASP.NET Core-integration *library* (as opposed to `Microsoft.NET.Sdk.Web`, which
  is for runnable apps).
- `test/Jsonapinator.Tests/`, `test/Jsonapinator.AspNetCore.Tests/` — xUnit, mirror the `src/`
  structure 1:1 by folder/namespace.
- `samples/` — four runnable `Microsoft.NET.Sdk.Web` apps, each demonstrating one thing:
  `Jsonapinator.Sample.ConventionBased`, `Jsonapinator.Sample.AttributeBased`,
  `Jsonapinator.Sample.ErrorHandling.Default`, `Jsonapinator.Sample.ErrorHandling.AlwaysMap`.
- `_docs/` — narrative docs: `attribute-based-mapping.md`, `convention-based-mapping.md`,
  `compound-documents.md`, `aspnetcore-integration.md`, `future-roadmap.md` (prioritized backlog
  of deferred features, security/perf/correctness findings — check here before assuming something
  isn't already a known gap).

## Core library architecture (`src/Jsonapinator/`)

Five layers, each with one responsibility, wired together only through `JsonApiSerializer`
(the public façade) and `IResourceTypeResolver` (the one seam everything else depends on):

- **`Document/`** — plain POCOs shaped exactly like the JSON:API spec (`JsonApiDocument`,
  `ResourceObject`, `ResourceIdentifierObject`, `RelationshipObject`, `ErrorObject`, ...). No
  reflection, no JSON library calls — pure data. `RelationshipObject`/`JsonApiDocumentData` use an
  `IsToMany`/`IsCollection` flag + two nullable members instead of `object`, to keep to-one/to-many
  and single/collection cases typed.
- **`Attributes/`** — the attribute-based mapping vocabulary: `[JsonApiResource]`, `[JsonApiId]`,
  `[JsonApiAttribute]` (opt-in per property — unmarked properties are silently skipped),
  `[JsonApiRelationship(name, RelationshipKind.ToOne|ToMany)]`.
- **`Metadata/`** — reflects a CLR type once into a `ResourceMetadata` (resource type name, id
  property, attribute list, relationship list), cached per-`Type` in a `ConcurrentDictionary`.
  **Two interchangeable implementations of `IResourceTypeResolver`**:
  - `ResourceTypeResolver` — reads the attributes above. Used by `new JsonApiSerializer()`.
  - `ConventionResourceTypeResolver` — zero attributes required. A property named exactly `Id`
    (of type `string`/`Guid`/`int`/`long`) becomes the id; a property whose type (or collection
    element type) is itself a class with its own `Id` property becomes a relationship; everything
    else becomes a flat attribute. No BCL scalar whitelist needed — `Guid`/`DateTime`/`decimal`
    naturally fail the "has an `Id` property" test. Used by `JsonApiSerializer.WithConventions()`.
  - Whichever resolver a `JsonApiSerializer` is constructed with is the *only* thing that changes
    between the two mapping modes — `ResourceGraphBuilder`, `JsonApiDocumentWriter`,
    `JsonApiDocumentReader`, and `ResourceMapper` are all resolver-agnostic.
- **`Serialization/`** (`ResourceGraphBuilder` + `JsonApiDocumentWriter`) — POCO → `Document`
  model → JSON string. `ResourceGraphBuilder` also walks `JsonApiDocumentOptions.Include`
  dot-notation paths (e.g. `"comments.author"`) against the real, already-in-memory relationship
  objects to populate compound documents (`included`) — no lazy-loading, no ORM awareness.
- **`Deserialization/`** (`JsonApiDocumentReader` + `ResourceMapper`) — JSON string → `Document`
  model → POCO. Mapping is presence-based (only JSON keys actually present get written), so it
  doubles as PATCH-semantics support. When an incoming document has an `"included"` array,
  relationships are fully hydrated (not just id/type stubs) via a cycle-safe recursive walk
  (`visiting` set — "currently on the call stack", not a permanent "seen" set, so the same
  resource reached via two different non-cyclic paths is still hydrated both times).
- **`JsonApiSerializer.cs`** — the public façade (`Serialize`/`SerializeCollection`/
  `SerializeErrors`/`Deserialize`/`DeserializeCollection`/`BuildDocument`/`ParseDocument`, plus
  non-generic overloads used by the ASP.NET Core formatters). One `JsonApiMappingException` type
  is used throughout for structural/programmer errors (missing attribute, malformed JSON, id type
  mismatch) — business-facing JSON:API errors are plain `ErrorObject` data instead, never
  exceptions.

## ASP.NET Core integration architecture (`src/Jsonapinator.AspNetCore/`)

- `JsonApiMvcBuilderExtensions.AddJsonApi()` is the one entry point. It builds a single shared
  `JsonApiSerializer` (convention-based by default — the *opposite* default from
  `new JsonApiSerializer()` in core), registers it as a DI singleton, inserts
  `Formatters/JsonApiInputFormatter`/`JsonApiOutputFormatter` ahead of the default System.Text.Json
  formatters, and wires `ErrorHandling/` into `ApiBehaviorOptions.InvalidModelStateResponseFactory`
  and `IExceptionHandler`.
- `Formatters/` — `TextInputFormatter`/`TextOutputFormatter` subclasses. Both claim
  `application/vnd.api+json` broadly (`CanRead/WriteType` return `true` unconditionally) because
  ASP.NET Core's formatter selector already gates on media type first; a genuinely unmappable POCO
  fails later via `JsonApiMappingException`, not at selection time. Bridge from
  `object`/`Type`-only formatter context into `JsonApiSerializer`'s generic API via the
  non-generic overloads in core (no `MakeGenericMethod` reflection in this project).
- `ErrorHandling/` — maps invalid `ModelState` (400) and unhandled exceptions (500) to JSON:API
  error documents. **Negotiation-aware by default**: only kicks in when the client's `Accept`
  header actually included `application/vnd.api+json` (`JsonApiNegotiation.WantsJsonApiErrors`);
  `JsonApiFormatterOptions.MapErrorsAlways()` forces it regardless. Exception messages/stack traces
  are **never** put in the response body — only logged server-side via `ILogger`.
- **`app.UseExceptionHandler()` must be called explicitly in `Program.cs`** for unhandled-exception
  mapping to actually run — `AddJsonApi()` can only register the `IExceptionHandler` as a DI
  service (it only has `IServiceCollection`/`IMvcBuilder`, not the built `IApplicationBuilder`).
  Every sample and the test app's `Program.cs` does this; new samples/hosts must too.
- `Jsonapinator.AspNetCore.Tests` has `InternalsVisibleTo` from the AspNetCore project so
  `ErrorHandling/`'s `internal` types can be unit-tested directly, not just through HTTP.

## Testing conventions

- One test file per production class, same relative path under `test/*/` as the class under
  `src/*/` (e.g. `src/Jsonapinator/Metadata/ResourceTypeResolver.cs` ↔
  `test/Jsonapinator.Tests/Metadata/ResourceTypeResolverTests.cs`).
- Test fixture POCOs (the resource classes being mapped) are usually private nested classes inside
  the test class that uses them, not shared across test files.
- `test/Jsonapinator.AspNetCore.Tests/EndToEnd/` uses `WebApplicationFactory<TestProgram>` for
  real-HTTP-pipeline tests (content negotiation, formatter selection, 400/500 behavior) alongside
  faster unit tests that hand-construct formatter contexts (`OutputFormatterWriteContext`,
  `InputFormatterContext`) directly — use the hand-constructed-context style for new formatter/
  error-handling logic, reserve `WebApplicationFactory` for proving the pipeline wiring itself.
- New non-generic or overload additions to `JsonApiSerializer`/`ResourceMapper` must be checked for
  C# overload-resolution collisions with existing generic methods (this has bitten this codebase
  twice: `Serialize<T>(T)` vs. `Serialize(object)`, and `Select(_mapper.Map<T>)` vs. `Map<T>`'s
  optional second parameter) — prefer an explicit lambda over a bare method-group when in doubt.
