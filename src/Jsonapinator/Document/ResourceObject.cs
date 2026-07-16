namespace Jsonapinator.Document;

/// <summary>
/// A JSON:API "resource object" (https://jsonapi.org/format/#document-resource-objects).
/// </summary>
public sealed class ResourceObject
{
    public required string Type { get; set; }

    public string? Id { get; set; }

    public IDictionary<string, object?>? Attributes { get; set; }

    public IDictionary<string, RelationshipObject>? Relationships { get; set; }

    public LinksObject? Links { get; set; }

    public MetaObject? Meta { get; set; }
}
