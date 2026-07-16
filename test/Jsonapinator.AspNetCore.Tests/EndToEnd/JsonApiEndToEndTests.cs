using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Jsonapinator.AspNetCore.Tests.EndToEnd;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jsonapinator.AspNetCore.Tests.EndToEnd;

public class JsonApiEndToEndTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public JsonApiEndToEndTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_with_json_api_accept_header_returns_a_json_api_response()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/articles/1");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.api+json", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"articles\"", body);
        Assert.Contains("\"id\":\"1\"", body);
    }

    [Fact]
    public async Task Post_with_json_api_content_type_and_valid_body_model_binds_correctly()
    {
        var client = _factory.CreateClient();
        var content = new StringContent(
            """{"data":{"type":"articles","id":"1","attributes":{"title":"Posted"}}}""",
            Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/articles") { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"title\":\"Posted\"", body);
    }

    [Fact]
    public async Task Post_with_a_malformed_json_api_body_returns_400()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("not json", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/articles") { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
