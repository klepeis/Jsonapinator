using System.Collections;
using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Jsonapinator.AspNetCore.Formatters;

/// <summary>
/// Writes controller action results as JSON:API documents for the "application/vnd.api+json"
/// media type, via the shared <see cref="JsonApiSerializer"/> registered by
/// <c>AddJsonApi()</c>. See <see cref="Serialization.ResourceGraphBuilder"/> for how
/// a POCO becomes a resource — this class only bridges ASP.NET Core's formatter pipeline to it.
/// </summary>
public sealed class JsonApiOutputFormatter : TextOutputFormatter
{
    public const string MediaType = "application/vnd.api+json";

    private readonly JsonApiSerializer _serializer;

    public JsonApiOutputFormatter(JsonApiSerializer serializer)
    {
        _serializer = serializer;
        SupportedMediaTypes.Add(MediaType);
        SupportedEncodings.Add(Encoding.UTF8);
    }

    /// <remarks>
    /// Safe to accept broadly: ASP.NET Core's formatter selector already narrows candidates to
    /// those whose <see cref="OutputFormatter.SupportedMediaTypes"/> matches the negotiated
    /// media type before <see cref="CanWriteType"/> is consulted, and no other registered
    /// formatter claims "application/vnd.api+json". A genuinely unmappable POCO fails later, at
    /// <see cref="JsonApiSerializer.Serialize(object, Jsonapinator.JsonApiDocumentOptions?)"/>/
    /// <see cref="JsonApiSerializer.SerializeCollection(IEnumerable, Jsonapinator.JsonApiDocumentOptions?)"/>,
    /// via <see cref="Exceptions.JsonApiMappingException"/> — the correct failure point.
    /// </remarks>
    protected override bool CanWriteType(Type? type) => type is not null;

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var json = context.Object is IEnumerable enumerable and not string
            ? _serializer.SerializeCollection(enumerable)
            : _serializer.Serialize(context.Object!);

        var response = context.HttpContext.Response;
        await using var writer = context.WriterFactory(response.Body, selectedEncoding);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
    }
}
