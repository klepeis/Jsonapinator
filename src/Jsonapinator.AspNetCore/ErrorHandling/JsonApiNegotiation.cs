using Jsonapinator.AspNetCore.Formatters;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Jsonapinator.AspNetCore.ErrorHandling;

/// <summary>
/// Shared "should this request get a JSON:API-shaped error document?" check, used by both
/// <see cref="JsonApiInvalidModelStateResponseFactory"/> and <see cref="JsonApiExceptionHandler"/>.
/// </summary>
internal static class JsonApiNegotiation
{
    internal static bool WantsJsonApiErrors(HttpRequest request, JsonApiFormatterOptions options)
    {
        if (options.AlwaysMapErrors)
        {
            return true;
        }

        var acceptValues = request.Headers.Accept;
        if (acceptValues.Count == 0)
        {
            return false;
        }

        if (!MediaTypeHeaderValue.TryParseList(acceptValues, out var parsed))
        {
            return false;
        }

        foreach (var mediaType in parsed)
        {
            if (mediaType.MediaType.Equals(JsonApiOutputFormatter.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
