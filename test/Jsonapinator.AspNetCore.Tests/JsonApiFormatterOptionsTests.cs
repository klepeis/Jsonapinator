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
}
