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
}
