using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.ErrorHandling;
using Microsoft.AspNetCore.Http;

namespace Jsonapinator.AspNetCore.Tests.ErrorHandling;

public class JsonApiNegotiationTests
{
    private static HttpRequest CreateRequest(string? accept = null)
    {
        var context = new DefaultHttpContext();
        if (accept is not null)
        {
            context.Request.Headers.Accept = accept;
        }

        return context.Request;
    }

    [Fact]
    public void WantsJsonApiErrors_true_when_always_map_and_no_accept_header()
    {
        var options = new JsonApiFormatterOptions().MapErrorsAlways();

        Assert.True(JsonApiNegotiation.WantsJsonApiErrors(CreateRequest(), options));
    }

    [Fact]
    public void WantsJsonApiErrors_false_when_not_always_map_and_no_accept_header()
    {
        var options = new JsonApiFormatterOptions();

        Assert.False(JsonApiNegotiation.WantsJsonApiErrors(CreateRequest(), options));
    }

    [Fact]
    public void WantsJsonApiErrors_true_for_a_single_matching_accept_value()
    {
        var options = new JsonApiFormatterOptions();

        Assert.True(JsonApiNegotiation.WantsJsonApiErrors(CreateRequest("application/vnd.api+json"), options));
    }

    [Fact]
    public void WantsJsonApiErrors_true_when_json_api_is_one_of_several_accept_values()
    {
        var options = new JsonApiFormatterOptions();

        Assert.True(JsonApiNegotiation.WantsJsonApiErrors(
            CreateRequest("application/json, application/vnd.api+json;q=0.9"), options));
    }

    [Fact]
    public void WantsJsonApiErrors_false_when_accept_present_but_non_matching()
    {
        var options = new JsonApiFormatterOptions();

        Assert.False(JsonApiNegotiation.WantsJsonApiErrors(CreateRequest("application/json"), options));
    }

    [Fact]
    public void WantsJsonApiErrors_false_not_throw_on_unparseable_accept_value()
    {
        var options = new JsonApiFormatterOptions();

        Assert.False(JsonApiNegotiation.WantsJsonApiErrors(CreateRequest("😀"), options));
    }
}
