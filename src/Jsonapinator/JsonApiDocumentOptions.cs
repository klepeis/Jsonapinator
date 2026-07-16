using Jsonapinator.Document;

namespace Jsonapinator;

/// <summary>
/// Optional document-level members to attach when serializing via <see cref="JsonApiSerializer"/>.
/// </summary>
public sealed class JsonApiDocumentOptions
{
    public MetaObject? Meta { get; init; }

    public LinksObject? Links { get; init; }

    /// <summary>
    /// Dot-notation relationship paths to include as compound-document resources
    /// (e.g. "author", "comments.author"). See https://jsonapi.org/format/#fetching-includes.
    /// </summary>
    public IEnumerable<string>? Include { get; init; }
}
