namespace Jsonapinator;

/// <summary>
/// Safety limits applied while hydrating relationships from untrusted JSON:API input (an
/// <c>"included"</c> array and/or to-many relationship <c>"data"</c> arrays). Defaults are
/// deliberately generous — large enough that no legitimate resource graph should ever hit them —
/// but give every consumer a finite ceiling out of the box instead of relying solely on
/// hosting-layer request-size limits.
/// </summary>
public sealed class JsonApiSerializerOptions
{
    /// <summary>
    /// Maximum number of relationship "hops" followed while hydrating a resource from an
    /// <c>"included"</c> array (deserialize) or while walking <c>Include</c> paths (serialize).
    /// Guards against a long linear chain of related resources (not a cycle — cycles are already
    /// guarded separately) driving unbounded recursion and an uncatchable
    /// <see cref="StackOverflowException"/>.
    /// </summary>
    public int MaxIncludeDepth { get; init; } = 32;

    /// <summary>
    /// Maximum number of resource objects allowed in a JSON:API document's <c>"included"</c>
    /// array during deserialize.
    /// </summary>
    public int MaxIncludedResources { get; init; } = 5000;

    /// <summary>
    /// Maximum number of resource identifiers allowed in a single to-many relationship's
    /// <c>"data"</c> array during deserialize.
    /// </summary>
    public int MaxToManyRelationshipSize { get; init; } = 5000;
}
