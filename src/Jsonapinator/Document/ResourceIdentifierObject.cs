namespace Jsonapinator.Document;

/// <summary>
/// A JSON:API "resource identifier object" (https://jsonapi.org/format/#document-resource-identifier-objects).
/// </summary>
public sealed class ResourceIdentifierObject
{
    public required string Type { get; set; }

    public required string Id { get; set; }

    public MetaObject? Meta { get; set; }
}
