namespace Jsonapinator.Attributes;

/// <summary>
/// Marks a property (of type exactly <see cref="Document.LinksObject"/>) as the supplier of a
/// named relationship's JSON:API relationship-level "links". <paramref name="relationshipName"/>
/// must match the <c>name</c> passed to the <see cref="JsonApiRelationshipAttribute"/> on the
/// actual relationship property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiRelationshipLinksAttribute : Attribute
{
    public JsonApiRelationshipLinksAttribute(string relationshipName)
    {
        RelationshipName = relationshipName;
    }

    public string RelationshipName { get; }
}
