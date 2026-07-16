using Jsonapinator.Document;

namespace Jsonapinator;

/// <summary>
/// Optional document-level members to attach when serializing via <see cref="JsonApiSerializer"/>.
/// </summary>
public sealed class JsonApiDocumentOptions
{
    public MetaObject? Meta { get; init; }

    public LinksObject? Links { get; init; }
}
