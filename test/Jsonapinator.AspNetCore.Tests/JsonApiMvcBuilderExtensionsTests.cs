using Jsonapinator;
using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.ErrorHandling;
using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Attributes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jsonapinator.AspNetCore.Tests;

public class JsonApiMvcBuilderExtensionsTests
{
    // No Jsonapinator.Attributes on purpose -> only mappable via convention-based resolution.
    private sealed class PlainArticle
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";
    }

    // Explicit [JsonApiAttribute]s but no [JsonApiId]/[JsonApiResource] convention "Id"-only
    // shape assumed -> convention-based resolution maps it fine too (it has a public "Id"
    // property), so to prove attribute-only vs. convention-only we rely on PlainArticle
    // (unmappable by attributes) and AttributeOnlyArticle (unmappable by convention because its
    // id property isn't named "Id").
    [JsonApiResource("articles")]
    private sealed class AttributeOnlyArticle
    {
        [JsonApiId]
        public string ArticleId { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "";
    }

    private static MvcOptions BuildMvcOptions(Action<JsonApiFormatterOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<MvcOptions>>().Value;
    }

    [Fact]
    public void AddJsonApi_registers_the_output_and_input_formatters()
    {
        var options = BuildMvcOptions();

        Assert.Contains(options.OutputFormatters, f => f is JsonApiOutputFormatter);
        Assert.Contains(options.InputFormatters, f => f is JsonApiInputFormatter);
    }

    [Fact]
    public void AddJsonApi_called_twice_registers_the_formatters_only_once()
    {
        var services = new ServiceCollection();
        var builder = services.AddControllers();
        builder.AddJsonApi();
        builder.AddJsonApi();
        var provider = services.BuildServiceProvider();

        var mvcOptions = provider.GetRequiredService<IOptions<MvcOptions>>().Value;
        Assert.Single(mvcOptions.OutputFormatters, f => f is JsonApiOutputFormatter);
        Assert.Single(mvcOptions.InputFormatters, f => f is JsonApiInputFormatter);
        Assert.Single(services, d => d.ServiceType == typeof(JsonApiSerializer));
        Assert.Single(services, d => d.ServiceType == typeof(JsonApiFormatterOptions));
    }

    [Fact]
    public void AddJsonApi_second_call_does_not_apply_a_different_configuration()
    {
        var services = new ServiceCollection();
        var builder = services.AddControllers();
        builder.AddJsonApi();
        builder.AddJsonApi(o => o.UseAttributes());
        var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        // Still convention-based (the first call's config) -- the second call is a no-op, proving
        // it doesn't silently reconfigure an already-registered JsonApiSerializer.
        var json = serializer.Serialize(new PlainArticle { Id = "1", Title = "Hello" });
        Assert.Contains("\"title\":\"Hello\"", json);
    }

    [Fact]
    public void AddJsonApi_registers_a_JsonApiSerializer_singleton()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();

        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        Assert.NotNull(serializer);
    }

    [Fact]
    public void AddJsonApi_default_uses_convention_based_mapping()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        var json = serializer.Serialize(new PlainArticle { Id = "1", Title = "Hello" });

        Assert.Contains("\"title\":\"Hello\"", json);
    }

    [Fact]
    public void AddJsonApi_default_does_not_use_attribute_based_mapping()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        // AttributeOnlyArticle's id property isn't named "Id", so convention-based resolution
        // (today's default) must fail on it.
        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => serializer.Serialize(new AttributeOnlyArticle { ArticleId = "1", Title = "Hello" }));
    }

    [Fact]
    public void AddJsonApi_UseAttributes_maps_a_poco_via_Jsonapinator_Attributes()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi(o => o.UseAttributes());
        var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        var json = serializer.Serialize(new AttributeOnlyArticle { ArticleId = "1", Title = "Hello" });

        Assert.Contains("\"title\":\"Hello\"", json);
    }

    [Fact]
    public void AddJsonApi_UseConventions_maps_a_plain_poco_with_no_attributes()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi(o => o.UseConventions());
        var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<JsonApiSerializer>();

        var json = serializer.Serialize(new PlainArticle { Id = "1", Title = "Hello" });

        Assert.Contains("\"title\":\"Hello\"", json);
    }

    [Fact]
    public void AddJsonApi_registers_the_JsonApiFormatterOptions_singleton()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<JsonApiFormatterOptions>());
    }

    [Fact]
    public void AddJsonApi_configures_InvalidModelStateResponseFactory_to_produce_json_api_errors_when_negotiated()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();
        var apiOptions = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Accept = "application/vnd.api+json";
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "Title is required.");
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);

        var result = Assert.IsType<ObjectResult>(apiOptions.InvalidModelStateResponseFactory!(actionContext));

        Assert.Equal(400, result.StatusCode);
        Assert.IsType<JsonApiErrorsPayload>(result.Value);
    }

    [Fact]
    public void AddJsonApi_registers_JsonApiExceptionHandler_as_an_IExceptionHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddJsonApi();
        var provider = services.BuildServiceProvider();

        var handlers = provider.GetRequiredService<IEnumerable<IExceptionHandler>>();

        Assert.Contains(handlers, h => h is JsonApiExceptionHandler);
    }
}
