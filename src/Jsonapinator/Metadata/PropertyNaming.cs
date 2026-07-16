namespace Jsonapinator.Metadata;

/// <summary>
/// Shared naming convention used by both <see cref="ResourceTypeResolver"/> and
/// <see cref="ConventionResourceTypeResolver"/> for default JSON names.
/// </summary>
internal static class PropertyNaming
{
    public static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
