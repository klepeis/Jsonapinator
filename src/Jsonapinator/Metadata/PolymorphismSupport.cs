using System.Reflection;
using System.Text.Json.Serialization;

namespace Jsonapinator.Metadata;

/// <summary>
/// Reflects a CLR type's <see cref="JsonPolymorphicAttribute"/>/<see cref="JsonDerivedTypeAttribute"/>
/// configuration (standard <c>System.Text.Json</c> polymorphism attributes — no Jsonapinator-specific
/// vocabulary) so both <see cref="IResourceTypeResolver"/> implementations can cache the result once
/// per type, alongside the rest of their metadata.
/// </summary>
internal static class PolymorphismSupport
{
    public static bool IsPolymorphic(Type type) =>
        type.IsDefined(typeof(JsonPolymorphicAttribute), inherit: false);

    /// <summary>
    /// Maps each declared string discriminator (e.g. <c>[JsonDerivedType(typeof(Video), "videos")]</c>)
    /// to its CLR type. Null if <paramref name="declaredType"/> isn't polymorphic. Derived-type
    /// registrations using a non-string (<c>int</c>) discriminator are skipped — JSON:API's own
    /// <c>"type"</c> member is always a string, so an int discriminator can never be matched against it.
    /// </summary>
    public static IReadOnlyDictionary<string, Type>? ResolveDerivedTypes(Type declaredType) =>
        IsPolymorphic(declaredType)
            ? declaredType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                .Where(a => a.TypeDiscriminator is string)
                .ToDictionary(a => (string)a.TypeDiscriminator!, a => a.DerivedType)
            : null;
}
