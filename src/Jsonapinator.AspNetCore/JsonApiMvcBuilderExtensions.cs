using Jsonapinator;
using Jsonapinator.AspNetCore;
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
    /// Adds JSON:API (<c>application/vnd.api+json</c>) support to MVC's formatter pipeline.
    /// Controller actions can return/accept plain POCOs mapped via <c>Jsonapinator.Attributes</c>
    /// (the default) or, via <c>configure(options => options.UseConventions())</c>, by convention.
    /// </summary>
    public static IMvcBuilder AddJsonApi(this IMvcBuilder builder, Action<JsonApiFormatterOptions>? configure = null)
    {
        var options = new JsonApiFormatterOptions();
        configure?.Invoke(options);

        var serializer = options.ConventionMapping
            ? JsonApiSerializer.WithConventions()
            : new JsonApiSerializer();

        builder.Services.AddSingleton(serializer);

        builder.AddMvcOptions(mvcOptions =>
        {
            mvcOptions.InputFormatters.Insert(0, new JsonApiInputFormatter(serializer));
            mvcOptions.OutputFormatters.Insert(0, new JsonApiOutputFormatter(serializer));
        });

        return builder;
    }
}
