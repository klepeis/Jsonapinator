using Jsonapinator;
using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Attributes;
using Microsoft.AspNetCore.Mvc;
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
}
