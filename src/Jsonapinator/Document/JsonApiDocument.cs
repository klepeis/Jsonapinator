namespace Jsonapinator.Document;

/// <summary>
/// The top-level JSON:API document (https://jsonapi.org/format/#document-top-level).
/// Per the spec, a document contains at least one of "data", "errors", or "meta" —
/// use the <c>For*</c> factory methods to construct a well-formed instance.
/// </summary>
public sealed class JsonApiDocument
{
    public JsonApiDocumentData? Data { get; set; }

    public List<ErrorObject>? Errors { get; set; }

    public MetaObject? Meta { get; set; }

    public LinksObject? Links { get; set; }

    public static JsonApiDocument ForSingleResource(ResourceObject? resource) => new()
    {
        Data = new JsonApiDocumentData { IsCollection = false, Single = resource },
    };

    public static JsonApiDocument ForCollection(IEnumerable<ResourceObject> resources) => new()
    {
        Data = new JsonApiDocumentData { IsCollection = true, Collection = resources.ToList() },
    };

    public static JsonApiDocument ForErrors(IEnumerable<ErrorObject> errors) => new()
    {
        Errors = errors.ToList(),
    };
}
