namespace Jsonapinator.Attributes;

/// <summary>
/// Marks a <see cref="string"/> property that overrides the resource's JSON:API <c>"type"</c>
/// name on a per-instance basis. When the property's runtime value is null or empty for a given
/// instance, the normal computed default (<see cref="JsonApiResourceAttribute.ResourceType"/> or
/// the camelCase class name) is used instead — the override is opt-in per instance.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiTypeAttribute : Attribute
{
}
