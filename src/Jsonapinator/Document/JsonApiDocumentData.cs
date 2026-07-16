namespace Jsonapinator.Document;

/// <summary>
/// The top-level "data" member of a JSON:API document. <see cref="IsCollection"/> determines
/// whether <see cref="Single"/> or <see cref="Collection"/> is the active member, mirroring the
/// spec's single-resource/resource-collection distinction.
/// </summary>
public sealed class JsonApiDocumentData
{
    public bool IsCollection { get; init; }

    public ResourceObject? Single { get; init; }

    public List<ResourceObject>? Collection { get; init; }
}
