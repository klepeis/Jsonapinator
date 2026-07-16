using System.Reflection;

namespace Jsonapinator.Metadata;

/// <summary>
/// Resolved, reflection-derived metadata for a CLR type mapped to a JSON:API resource.
/// Built once per type by <see cref="IResourceTypeResolver"/> and consumed by both the
/// serialization and deserialization layers.
/// </summary>
public sealed class ResourceMetadata
{
    public required Type ClrType { get; init; }

    public required string ResourceType { get; init; }

    public required PropertyInfo IdProperty { get; init; }

    public required IReadOnlyList<AttributeMetadata> Attributes { get; init; }

    public required IReadOnlyList<RelationshipMetadata> Relationships { get; init; }

    /// <summary>
    /// The property (of type <see cref="Document.MetaObject"/>) that supplies this resource's
    /// JSON:API resource-level "meta", if one was declared. Null if none.
    /// </summary>
    public PropertyInfo? MetaProperty { get; init; }

    /// <summary>
    /// The property (of type <see cref="Document.LinksObject"/>) that supplies this resource's
    /// JSON:API resource-level "links", if one was declared. Null if none.
    /// </summary>
    public PropertyInfo? LinksProperty { get; init; }

    /// <summary>
    /// The property (of type <see cref="string"/>) that overrides <see cref="ResourceType"/> on
    /// a per-instance basis, if one was declared. Null if none — see
    /// <see cref="Attributes.JsonApiTypeAttribute"/>.
    /// </summary>
    public PropertyInfo? TypeProperty { get; init; }
}
