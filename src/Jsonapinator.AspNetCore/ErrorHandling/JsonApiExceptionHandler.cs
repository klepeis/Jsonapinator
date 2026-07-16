using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Document;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jsonapinator.AspNetCore.ErrorHandling;

/// <summary>
/// Maps unhandled exceptions to a generic JSON:API error document when
/// <c>application/vnd.api+json</c> was negotiated (or <see cref="JsonApiFormatterOptions.MapErrorsAlways"/>
/// was used) — full exception details are always logged server-side but never included in the
/// response body. Registered by <c>AddJsonApi()</c> via <c>services.AddExceptionHandler&lt;JsonApiExceptionHandler&gt;()</c>;
/// requires <c>app.UseExceptionHandler()</c> to be called explicitly in the application pipeline
/// for this to actually run — see _docs/aspnetcore-integration.md.
/// </summary>
public sealed class JsonApiExceptionHandler : IExceptionHandler
{
    private readonly JsonApiSerializer _serializer;
    private readonly JsonApiFormatterOptions _options;
    private readonly ILogger<JsonApiExceptionHandler> _logger;

    public JsonApiExceptionHandler(JsonApiSerializer serializer, JsonApiFormatterOptions options, ILogger<JsonApiExceptionHandler> logger)
    {
        _serializer = serializer;
        _options = options;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        if (!JsonApiNegotiation.WantsJsonApiErrors(httpContext.Request, _options))
        {
            return false;
        }

        var errors = new[] { new ErrorObject { Status = "500", Title = "An unexpected error occurred." } };

        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = JsonApiOutputFormatter.MediaType;
        await httpContext.Response.WriteAsync(_serializer.SerializeErrors(errors), cancellationToken);

        return true;
    }
}
