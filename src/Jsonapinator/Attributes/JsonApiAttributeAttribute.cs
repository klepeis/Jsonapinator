namespace Jsonapinator.Attributes;

/// <summary>
/// Marks a property as a JSON:API "attribute". V1 requires this attribute explicitly on every
/// property that should be serialized — properties are not included by convention, to avoid
/// leaking unintended data.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiAttributeAttribute : Attribute
{
}
