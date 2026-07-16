using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Jsonapinator.AspNetCore.Tests.ErrorHandling;

public class JsonApiInvalidModelStateResponseFactoryTests
{
    private static ActionContext CreateActionContext(string? accept, ModelStateDictionary modelState)
    {
        var httpContext = new DefaultHttpContext();
        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
    }

    private static ModelStateDictionary OneError(string key, string message)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(key, message);
        return modelState;
    }

    [Fact]
    public void Create_builds_a_400_json_api_errors_result_when_negotiated()
    {
        var options = new JsonApiFormatterOptions();
        var factory = JsonApiInvalidModelStateResponseFactory.Create(options, _ => throw new InvalidOperationException("fallback should not run"));
        var context = CreateActionContext("application/vnd.api+json", OneError("Title", "Title is required."));

        var result = Assert.IsType<ObjectResult>(factory(context));

        Assert.Equal(400, result.StatusCode);
        Assert.Contains("application/vnd.api+json", result.ContentTypes);
        var payload = Assert.IsType<JsonApiErrorsPayload>(result.Value);
        var error = Assert.Single(payload.Errors);
        Assert.Equal("400", error.Status);
        Assert.Equal("Validation Failed", error.Title);
        Assert.Equal("Title is required.", error.Detail);
        Assert.Equal("/data/attributes/title", error.Source!.Pointer);
    }

    [Fact]
    public void Create_builds_one_error_per_individual_model_error()
    {
        var options = new JsonApiFormatterOptions();
        var factory = JsonApiInvalidModelStateResponseFactory.Create(options, _ => throw new InvalidOperationException());
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "Title is required.");
        modelState.AddModelError("Title", "Title is too long.");
        modelState.AddModelError("WordCount", "WordCount must be positive.");
        var context = CreateActionContext("application/vnd.api+json", modelState);

        var result = Assert.IsType<ObjectResult>(factory(context));

        var payload = Assert.IsType<JsonApiErrorsPayload>(result.Value);
        Assert.Equal(3, payload.Errors.Count);
    }

    [Fact]
    public void Create_maps_an_empty_key_to_the_data_pointer()
    {
        var options = new JsonApiFormatterOptions();
        var factory = JsonApiInvalidModelStateResponseFactory.Create(options, _ => throw new InvalidOperationException());
        var context = CreateActionContext("application/vnd.api+json", OneError("", "The body is invalid."));

        var result = Assert.IsType<ObjectResult>(factory(context));

        var payload = Assert.IsType<JsonApiErrorsPayload>(result.Value);
        Assert.Equal("/data", payload.Errors[0].Source!.Pointer);
    }

    [Fact]
    public void Create_falls_back_to_the_fallback_factory_when_not_negotiated_and_not_always_map()
    {
        var options = new JsonApiFormatterOptions();
        var sentinel = new ObjectResult(null) { StatusCode = 599 };
        var factory = JsonApiInvalidModelStateResponseFactory.Create(options, _ => sentinel);
        var context = CreateActionContext(accept: null, OneError("Title", "Title is required."));

        var result = factory(context);

        Assert.Same(sentinel, result);
    }

    [Fact]
    public void Create_converts_even_without_a_matching_accept_header_when_always_map_is_set()
    {
        var options = new JsonApiFormatterOptions().MapErrorsAlways();
        var factory = JsonApiInvalidModelStateResponseFactory.Create(options, _ => throw new InvalidOperationException("fallback should not run"));
        var context = CreateActionContext(accept: null, OneError("Title", "Title is required."));

        var result = Assert.IsType<ObjectResult>(factory(context));

        Assert.IsType<JsonApiErrorsPayload>(result.Value);
    }
}
