namespace Jsonapinator.Attributes;

/// <summary>
/// Declares the JSON:API resource type name for a POCO (e.g. "articles").
/// Required on any type passed to <see cref="Jsonapinator.JsonApiSerializer"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiResourceAttribute : Attribute
{
    public JsonApiResourceAttribute(string resourceType)
    {
        ResourceType = resourceType;
    }

    public string ResourceType { get; }
}
