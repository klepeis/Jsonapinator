using System.Collections;
using System.Text;
using Jsonapinator.Exceptions;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Jsonapinator.AspNetCore.Formatters;

/// <summary>
/// Reads request bodies as JSON:API documents for the "application/vnd.api+json" media type,
/// via the shared <see cref="JsonApiSerializer"/> registered by <c>AddJsonApi()</c>.
/// </summary>
public sealed class JsonApiInputFormatter : TextInputFormatter
{
    private readonly JsonApiSerializer _serializer;

    public JsonApiInputFormatter(JsonApiSerializer serializer)
    {
        _serializer = serializer;
        SupportedMediaTypes.Add(JsonApiOutputFormatter.MediaType);
        SupportedEncodings.Add(Encoding.UTF8);
    }

    /// <remarks>
    /// Safe to accept any type for the same reason <see cref="JsonApiOutputFormatter.CanWriteType"/>
    /// is: the formatter selector already gates on media type before this is consulted, and no
    /// other formatter claims "application/vnd.api+json".
    /// </remarks>
    protected override bool CanReadType(Type type) => true;

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        using var reader = context.ReaderFactory(context.HttpContext.Request.Body, encoding);
        var json = await reader.ReadToEndAsync();

        try
        {
            var elementType = GetCollectionElementType(context.ModelType);
            object model = elementType is not null
                ? AdaptCollection(_serializer.DeserializeCollection(elementType, json), context.ModelType, elementType)
                : _serializer.Deserialize(context.ModelType, json);

            return await InputFormatterResult.SuccessAsync(model);
        }
        catch (JsonApiMappingException ex)
        {
            context.ModelState.TryAddModelError(context.ModelName, ex.Message);
            return await InputFormatterResult.FailureAsync();
        }
    }

    private static Type? GetCollectionElementType(Type modelType)
    {
        if (modelType.IsArray)
        {
            return modelType.GetElementType();
        }

        if (modelType.IsGenericType)
        {
            var definition = modelType.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) ||
                definition == typeof(ICollection<>) || definition == typeof(IEnumerable<>) ||
                definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>))
            {
                return modelType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static object AdaptCollection(IList list, Type modelType, Type elementType)
    {
        if (!modelType.IsArray)
        {
            return list;
        }

        var array = Array.CreateInstance(elementType, list.Count);
        list.CopyTo(array, 0);
        return array;
    }
}
