using System.Reflection;

namespace Jsonapinator.Metadata;

/// <summary>
/// Resolved metadata for a single JSON:API attribute property.
/// </summary>
public sealed class AttributeMetadata
{
    public required PropertyInfo Property { get; init; }

    public required string JsonName { get; init; }
}
