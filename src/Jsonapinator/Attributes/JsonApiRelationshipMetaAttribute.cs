namespace Jsonapinator.Attributes;

/// <summary>
/// Marks a property (of type exactly <see cref="Document.MetaObject"/>) as the supplier of a
/// named relationship's JSON:API relationship-level "meta". <paramref name="relationshipName"/>
/// must match the <c>name</c> passed to the <see cref="JsonApiRelationshipAttribute"/> on the
/// actual relationship property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiRelationshipMetaAttribute : Attribute
{
    public JsonApiRelationshipMetaAttribute(string relationshipName)
    {
        RelationshipName = relationshipName;
    }

    public string RelationshipName { get; }
}
