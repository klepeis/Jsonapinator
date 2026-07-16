using Jsonapinator.AspNetCore;

namespace Jsonapinator.AspNetCore.Tests;

public class JsonApiFormatterOptionsTests
{
    [Fact]
    public void UseConventions_returns_itself_fluently()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.UseConventions();

        Assert.Same(options, result);
    }

    [Fact]
    public void UseAttributes_returns_itself_fluently()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.UseAttributes();

        Assert.Same(options, result);
    }

    [Fact]
    public void MapErrorsAlways_returns_itself_fluently()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.MapErrorsAlways();

        Assert.Same(options, result);
    }

    [Fact]
    public void AlwaysMapErrors_defaults_to_false_and_becomes_true_after_MapErrorsAlways()
    {
        var options = new JsonApiFormatterOptions();

        Assert.False(options.AlwaysMapErrors);

        options.MapErrorsAlways();

        Assert.True(options.AlwaysMapErrors);
    }

    [Fact]
    public void WithMaxIncludeDepth_returns_itself_fluently_and_updates_SerializerOptions()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.WithMaxIncludeDepth(5);

        Assert.Same(options, result);
        Assert.Equal(5, options.SerializerOptions.MaxIncludeDepth);
    }

    [Fact]
    public void WithMaxIncludedResources_returns_itself_fluently_and_updates_SerializerOptions()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.WithMaxIncludedResources(10);

        Assert.Same(options, result);
        Assert.Equal(10, options.SerializerOptions.MaxIncludedResources);
    }

    [Fact]
    public void WithMaxToManyRelationshipSize_returns_itself_fluently_and_updates_SerializerOptions()
    {
        var options = new JsonApiFormatterOptions();

        var result = options.WithMaxToManyRelationshipSize(15);

        Assert.Same(options, result);
        Assert.Equal(15, options.SerializerOptions.MaxToManyRelationshipSize);
    }

    [Fact]
    public void Chained_limit_setters_preserve_each_others_values()
    {
        var options = new JsonApiFormatterOptions()
            .WithMaxIncludeDepth(5)
            .WithMaxIncludedResources(10)
            .WithMaxToManyRelationshipSize(15);

        Assert.Equal(5, options.SerializerOptions.MaxIncludeDepth);
        Assert.Equal(10, options.SerializerOptions.MaxIncludedResources);
        Assert.Equal(15, options.SerializerOptions.MaxToManyRelationshipSize);
    }
}
