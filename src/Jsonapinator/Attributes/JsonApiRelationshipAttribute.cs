namespace Jsonapinator.Attributes;

/// <summary>
/// Marks a navigation property as a JSON:API relationship. The property's CLR type supplies
/// the related resource's type (for id/type extraction); the related object graph itself is
/// not serialized (compound documents are out of scope for V1).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiRelationshipAttribute : Attribute
{
    public JsonApiRelationshipAttribute(string name, RelationshipKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public RelationshipKind Kind { get; }
}
