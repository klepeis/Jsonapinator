namespace Jsonapinator.Attributes;

/// <summary>
/// Marks the property that supplies a resource's JSON:API "id". The property's CLR type
/// must be <see cref="string"/>, <see cref="Guid"/>, <see cref="int"/>, or <see cref="long"/> —
/// it is always serialized as a JSON string per the spec.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiIdAttribute : Attribute
{
}
