using Jsonapinator;
using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.ErrorHandling;
using Jsonapinator.AspNetCore.Formatters;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Jsonapinator's JSON:API input/output formatters (<c>application/vnd.api+json</c>)
/// with ASP.NET Core MVC's content-negotiation pipeline.
/// </summary>
public static class JsonApiMvcBuilderExtensions
{
    /// <summary>
    /// Adds JSON:API (<c>application/vnd.api+json</c>) support to MVC's formatter pipeline, plus
    /// JSON:API error-document mapping for invalid <c>ModelState</c> and unhandled exceptions
    /// (negotiation-aware by default — see <see cref="JsonApiFormatterOptions.MapErrorsAlways"/>).
    /// Controller actions can return/accept plain POCOs mapped by convention (the default) or,
    /// via <c>configure(options => options.UseAttributes())</c>, via <c>Jsonapinator.Attributes</c>.
    /// Unhandled-exception mapping additionally requires <c>app.UseExceptionHandler()</c> to be
    /// called in the application pipeline — see _docs/aspnetcore-integration.md.
    /// </summary>
    public static IMvcBuilder AddJsonApi(this IMvcBuilder builder, Action<JsonApiFormatterOptions>? configure = null)
    {
        var options = new JsonApiFormatterOptions();
        configure?.Invoke(options);

        var serializer = options.ConventionMapping
            ? JsonApiSerializer.WithConventions()
            : new JsonApiSerializer();

        builder.Services.AddSingleton(serializer);
        builder.Services.AddSingleton(options);

        builder.AddMvcOptions(mvcOptions =>
        {
            mvcOptions.InputFormatters.Insert(0, new JsonApiInputFormatter(serializer));
            mvcOptions.OutputFormatters.Insert(0, new JsonApiOutputFormatter(serializer));
        });

        builder.Services.Configure<ApiBehaviorOptions>(apiOptions =>
        {
            var fallbackFactory = apiOptions.InvalidModelStateResponseFactory;
            apiOptions.InvalidModelStateResponseFactory =
                JsonApiInvalidModelStateResponseFactory.Create(options, fallbackFactory);
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<JsonApiExceptionHandler>();

        return builder;
    }
}
