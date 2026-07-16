using System.Reflection;
using Jsonapinator.Attributes;

namespace Jsonapinator.Metadata;

/// <summary>
/// Resolved metadata for a single JSON:API relationship property.
/// </summary>
public sealed class RelationshipMetadata
{
    public required PropertyInfo Property { get; init; }

    public required string Name { get; init; }

    public required RelationshipKind Kind { get; init; }

    /// <summary>
    /// The related resource's CLR type — the property type itself for to-one relationships,
    /// or the element type for to-many relationships.
    /// </summary>
    public required Type RelatedClrType { get; init; }
}
