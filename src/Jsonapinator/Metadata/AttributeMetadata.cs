using System.Reflection;

namespace Jsonapinator.Metadata;

/// <summary>
/// Resolved metadata for a single JSON:API attribute property.
/// </summary>
public sealed class AttributeMetadata
{
    public required PropertyInfo Property { get; init; }

    public required string JsonName { get; init; }

    /// <summary>
    /// True when <see cref="Property"/>'s declared type is decorated with
    /// <see cref="System.Text.Json.Serialization.JsonPolymorphicAttribute"/> — see
    /// <see cref="PolymorphismSupport"/>.
    /// </summary>
    public bool IsPolymorphic { get; init; }
}
