using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Document;
using Microsoft.AspNetCore.Mvc;

namespace Jsonapinator.AspNetCore.ErrorHandling;

/// <summary>
/// Builds the <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/> replacement
/// registered by <c>AddJsonApi()</c> — maps invalid <c>ModelState</c> to a JSON:API errors
/// document when negotiated, otherwise delegates to the framework's own default factory.
/// </summary>
internal static class JsonApiInvalidModelStateResponseFactory
{
    internal static Func<ActionContext, IActionResult> Create(
        JsonApiFormatterOptions options,
        Func<ActionContext, IActionResult> fallbackFactory)
    {
        return actionContext =>
        {
            if (!JsonApiNegotiation.WantsJsonApiErrors(actionContext.HttpContext.Request, options))
            {
                return fallbackFactory(actionContext);
            }

            var errors = new List<ErrorObject>();
            foreach (var (key, entry) in actionContext.ModelState)
            {
                if (entry is null)
                {
                    continue;
                }

                foreach (var error in entry.Errors)
                {
                    errors.Add(new ErrorObject
                    {
                        Status = "400",
                        Title = "Validation Failed",
                        Detail = error.ErrorMessage,
                        Source = new ErrorSourceObject { Pointer = ToAttributePointer(key) },
                    });
                }
            }

            var result = new ObjectResult(new JsonApiErrorsPayload(errors)) { StatusCode = 400 };
            result.ContentTypes.Add(JsonApiOutputFormatter.MediaType);
            return result;
        };
    }

    private static string ToAttributePointer(string modelStateKey) =>
        string.IsNullOrEmpty(modelStateKey)
            ? "/data"
            : $"/data/attributes/{char.ToLowerInvariant(modelStateKey[0])}{modelStateKey[1..]}";
}
