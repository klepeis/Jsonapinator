using System.Text.Json;

namespace Jsonapinator;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for serializing/deserializing a nested attribute
/// value's own properties (as opposed to the attribute's own JSON:API key, which is always
/// camelCased separately via <see cref="Metadata.PropertyNaming.ToCamelCase"/>). CamelCase keeps a
/// nested object attribute's own properties consistent with the attribute key convention;
/// case-insensitive read means a document written before this policy existed (raw PascalCase
/// nested keys) still deserializes correctly.
/// </summary>
internal static class NestedValueSerialization
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
