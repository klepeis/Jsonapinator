using System.Text;
using Jsonapinator;
using Jsonapinator.AspNetCore.Formatters;
using Jsonapinator.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Jsonapinator.AspNetCore.Tests.Formatters;

public class JsonApiInputFormatterTests
{
    [JsonApiResource("articles")]
    private sealed class Article
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "";
    }

    private static readonly EmptyModelMetadataProvider MetadataProvider = new();

    private readonly JsonApiInputFormatter _formatter = new(new JsonApiSerializer());

    private static InputFormatterContext CreateContext(string json, Type modelType)
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var httpContext = new DefaultHttpContext { Request = { Body = body, ContentLength = body.Length } };

        return new InputFormatterContext(
            httpContext,
            modelName: "model",
            modelState: new ModelStateDictionary(),
            metadata: MetadataProvider.GetMetadataForType(modelType),
            readerFactory: (stream, encoding) => new StreamReader(stream, encoding));
    }

    [Fact]
    public void Registers_the_json_api_media_type_and_utf8_encoding()
    {
        Assert.Contains(_formatter.SupportedMediaTypes, m => m == JsonApiOutputFormatter.MediaType);
        Assert.Contains(_formatter.SupportedEncodings, e => Equals(e, Encoding.UTF8));
    }

    [Fact]
    public async Task ReadRequestBodyAsync_deserializes_a_single_resource_body()
    {
        var context = CreateContext("""{"data":{"type":"articles","id":"1","attributes":{"title":"Hello"}}}""", typeof(Article));

        var result = await _formatter.ReadRequestBodyAsync(context, Encoding.UTF8);

        Assert.False(result.HasError);
        var article = Assert.IsType<Article>(result.Model);
        Assert.Equal("1", article.Id);
        Assert.Equal("Hello", article.Title);
    }

    [Theory]
    [InlineData(typeof(List<Article>))]
    [InlineData(typeof(Article[]))]
    [InlineData(typeof(IEnumerable<Article>))]
    public async Task ReadRequestBodyAsync_deserializes_a_collection_body(Type modelType)
    {
        var context = CreateContext("""{"data":[{"type":"articles","id":"1"},{"type":"articles","id":"2"}]}""", modelType);

        var result = await _formatter.ReadRequestBodyAsync(context, Encoding.UTF8);

        Assert.False(result.HasError);
        var articles = Assert.IsAssignableFrom<IEnumerable<Article>>(result.Model).ToList();
        Assert.Equal(2, articles.Count);
        Assert.Equal("2", articles[1].Id);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_returns_failure_for_a_mapping_invalid_body()
    {
        var context = CreateContext("not json", typeof(Article));

        var result = await _formatter.ReadRequestBodyAsync(context, Encoding.UTF8);

        Assert.True(result.HasError);
        Assert.False(context.ModelState.IsValid);
    }
}
