using Jsonapinator.Deserialization;
using Jsonapinator.Document;
using Jsonapinator.Metadata;
using Jsonapinator.Serialization;

namespace Jsonapinator;

/// <summary>
/// Public façade for converting between .NET POCOs (mapped via <c>Jsonapinator.Attributes</c>)
/// and JSON:API documents. This is the primary entry point most consumers need; the
/// <see cref="BuildDocument"/>/<see cref="ParseDocument"/> methods are an escape hatch onto the
/// underlying spec-shaped <see cref="Document"/> model for callers who need finer control
/// (e.g. attaching per-resource links/meta before writing).
/// </summary>
public sealed class JsonApiSerializer
{
    private readonly ResourceGraphBuilder _graphBuilder;
    private readonly IJsonApiWriter _writer;
    private readonly IJsonApiReader _reader;
    private readonly ResourceMapper _mapper;

    public JsonApiSerializer()
        : this(new ResourceTypeResolver(), new JsonApiDocumentWriter(), new JsonApiDocumentReader())
    {
    }

    /// <summary>
    /// Creates a <see cref="JsonApiSerializer"/> that maps POCOs by convention — no
    /// <c>Jsonapinator.Attributes</c> required. See <see cref="ConventionResourceTypeResolver"/>
    /// for the classification rule.
    /// </summary>
    public static JsonApiSerializer WithConventions() =>
        new(new ConventionResourceTypeResolver(), new JsonApiDocumentWriter(), new JsonApiDocumentReader());

    public JsonApiSerializer(IResourceTypeResolver resolver, IJsonApiWriter writer, IJsonApiReader reader)
    {
        _graphBuilder = new ResourceGraphBuilder(resolver);
        _writer = writer;
        _reader = reader;
        _mapper = new ResourceMapper(resolver);
    }

    public string Serialize<T>(T resource, JsonApiDocumentOptions? options = null) where T : notnull
    {
        var document = options?.Include is { } includePaths
            ? _graphBuilder.BuildDocument(resource, includePaths)
            : BuildDocument(resource);
        ApplyOptions(document, options);
        return _writer.Write(document);
    }

    public string SerializeCollection<T>(IEnumerable<T> resources, JsonApiDocumentOptions? options = null) where T : notnull
    {
        var document = options?.Include is { } includePaths
            ? _graphBuilder.BuildCollectionDocument(resources, includePaths)
            : _graphBuilder.BuildCollectionDocument(resources);
        ApplyOptions(document, options);
        return _writer.Write(document);
    }

    public string SerializeErrors(IEnumerable<ErrorObject> errors) =>
        _writer.Write(JsonApiDocument.ForErrors(errors));

    public T Deserialize<T>(string json) where T : new()
    {
        var document = ParseDocument(json);
        return _mapper.Map<T>(document.Data!.Single!, document.Included);
    }

    public IReadOnlyList<T> DeserializeCollection<T>(string json) where T : new()
    {
        var document = ParseDocument(json);
        return document.Data!.Collection!.Select(resource => _mapper.Map<T>(resource, document.Included)).ToList();
    }

    public JsonApiDocument BuildDocument<T>(T resource) where T : notnull =>
        _graphBuilder.BuildDocument(resource);

    public JsonApiDocument BuildDocument<T>(T resource, IEnumerable<string>? includePaths) where T : notnull =>
        _graphBuilder.BuildDocument(resource, includePaths);

    public JsonApiDocument ParseDocument(string json) => _reader.Read(json);

    private static void ApplyOptions(JsonApiDocument document, JsonApiDocumentOptions? options)
    {
        if (options is null)
        {
            return;
        }

        document.Meta = options.Meta;
        document.Links = options.Links;
    }
}
