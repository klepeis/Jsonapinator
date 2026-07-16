using System.Reflection;
using System.Text;
using Jsonapinator;
using Jsonapinator.AspNetCore.ErrorHandling;
using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Jsonapinator.AspNetCore.Tests.Formatters;

public class JsonApiOutputFormatterTests
{
    [JsonApiResource("articles")]
    private sealed class Article
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "";
    }

    private readonly JsonApiOutputFormatter _formatter = new(new JsonApiSerializer());

    private static bool InvokeCanWriteType(JsonApiOutputFormatter formatter, Type? type)
    {
        var method = typeof(JsonApiOutputFormatter).GetMethod(
            "CanWriteType", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(formatter, new object?[] { type })!;
    }

    private static OutputFormatterWriteContext CreateContext(object? value, Type objectType, Stream body)
    {
        var httpContext = new DefaultHttpContext { Response = { Body = body } };
        return new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding, leaveOpen: true),
            objectType,
            value);
    }

    [Fact]
    public void Registers_the_json_api_media_type_and_utf8_encoding()
    {
        Assert.Contains(_formatter.SupportedMediaTypes, m => m == "application/vnd.api+json");
        Assert.Contains(_formatter.SupportedEncodings, e => Equals(e, Encoding.UTF8));
    }

    [Fact]
    public void CanWriteType_returns_true_for_a_non_null_type()
    {
        Assert.True(InvokeCanWriteType(_formatter, typeof(Article)));
    }

    [Fact]
    public void CanWriteType_returns_false_for_a_null_type()
    {
        Assert.False(InvokeCanWriteType(_formatter, null));
    }

    [Fact]
    public async Task WriteResponseBodyAsync_writes_a_single_resource_document()
    {
        var article = new Article { Id = "1", Title = "Hello" };
        using var body = new MemoryStream();
        var context = CreateContext(article, typeof(Article), body);

        await _formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        body.Position = 0;
        var json = new StreamReader(body, Encoding.UTF8).ReadToEnd();
        var roundTripped = new JsonApiSerializer().Deserialize<Article>(json);
        Assert.Equal("1", roundTripped.Id);
        Assert.Equal("Hello", roundTripped.Title);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_writes_a_collection_document_for_an_enumerable_object()
    {
        var articles = new List<Article> { new() { Id = "1" }, new() { Id = "2" } };
        using var body = new MemoryStream();
        var context = CreateContext(articles, typeof(List<Article>), body);

        await _formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        body.Position = 0;
        var json = new StreamReader(body, Encoding.UTF8).ReadToEnd();
        var roundTripped = new JsonApiSerializer().DeserializeCollection<Article>(json);
        Assert.Equal(2, roundTripped.Count);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_writes_an_errors_document_for_a_JsonApiErrorsPayload()
    {
        var payload = new JsonApiErrorsPayload(new List<ErrorObject> { new() { Status = "400", Title = "Validation Failed" } });
        using var body = new MemoryStream();
        var context = CreateContext(payload, typeof(JsonApiErrorsPayload), body);

        await _formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        body.Position = 0;
        var json = new StreamReader(body, Encoding.UTF8).ReadToEnd();
        Assert.Contains("\"errors\"", json);
        Assert.DoesNotContain("\"data\"", json);
    }
}
