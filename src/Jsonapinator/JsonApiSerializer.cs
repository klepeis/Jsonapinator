using System.Collections;
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

    /// <summary>
    /// Non-generic bridge for callers that only have a runtime <see cref="Type"/>, not a
    /// compile-time <c>T</c> (e.g. an ASP.NET Core output formatter). Prefer <see cref="Serialize{T}"/>
    /// when <c>T</c> is known at the call site — C# overload resolution always prefers the
    /// generic exact match over this one.
    /// </summary>
    public string Serialize(object resource, JsonApiDocumentOptions? options = null)
    {
        var document = options?.Include is { } includePaths
            ? _graphBuilder.BuildDocument(resource, includePaths)
            : _graphBuilder.BuildDocument(resource);
        ApplyOptions(document, options);
        return _writer.Write(document);
    }

    /// <summary>
    /// Non-generic bridge for callers that only have a runtime <see cref="Type"/>, not a
    /// compile-time <c>T</c> (e.g. an ASP.NET Core output formatter). Prefer
    /// <see cref="SerializeCollection{T}"/> when <c>T</c> is known at the call site.
    /// </summary>
    public string SerializeCollection(IEnumerable resources, JsonApiDocumentOptions? options = null)
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

    /// <summary>
    /// Non-generic bridge for callers that only have a runtime <see cref="Type"/>, not a
    /// compile-time <c>T</c> (e.g. an ASP.NET Core input formatter). Prefer <see cref="Deserialize{T}"/>
    /// when <c>T</c> is known at the call site.
    /// </summary>
    public object Deserialize(Type resourceType, string json)
    {
        var document = ParseDocument(json);
        return _mapper.Map(resourceType, document.Data!.Single!, document.Included);
    }

    /// <summary>
    /// Non-generic bridge for callers that only have a runtime <see cref="Type"/>, not a
    /// compile-time <c>T</c> (e.g. an ASP.NET Core input formatter). Prefer
    /// <see cref="DeserializeCollection{T}"/> when <c>T</c> is known at the call site. The
    /// returned list is a concrete <c>List&lt;resourceType&gt;</c>, directly assignable to
    /// <c>List&lt;T&gt;</c>/<c>IEnumerable&lt;T&gt;</c>/<c>IList&lt;T&gt;</c>/<c>ICollection&lt;T&gt;</c>/
    /// <c>IReadOnlyList&lt;T&gt;</c>/<c>IReadOnlyCollection&lt;T&gt;</c>.
    /// </summary>
    public IList DeserializeCollection(Type resourceType, string json)
    {
        var document = ParseDocument(json);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType))!;
        foreach (var resourceObject in document.Data!.Collection!)
        {
            list.Add(_mapper.Map(resourceType, resourceObject, document.Included));
        }

        return list;
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
