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
}
