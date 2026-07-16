namespace Jsonapinator.Document;

/// <summary>
/// A JSON:API "relationship object" (https://jsonapi.org/format/#document-resource-object-relationships).
/// <see cref="IsToMany"/> determines whether <see cref="SingleData"/> or <see cref="ManyData"/> is the
/// active member — this mirrors the spec's to-one/to-many distinction without resorting to an
/// untyped <c>object</c> data member.
/// </summary>
public sealed class RelationshipObject
{
    public bool IsToMany { get; set; }

    public ResourceIdentifierObject? SingleData { get; set; }

    public List<ResourceIdentifierObject>? ManyData { get; set; }

    public LinksObject? Links { get; set; }

    public MetaObject? Meta { get; set; }
}
