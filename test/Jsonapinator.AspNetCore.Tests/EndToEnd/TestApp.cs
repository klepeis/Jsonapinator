using Jsonapinator.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jsonapinator.AspNetCore.Tests.EndToEnd;

[JsonApiResource("articles")]
public sealed class TestArticle
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Title { get; set; } = "";
}

[ApiController]
[Route("articles")]
public sealed class TestArticlesController : ControllerBase
{
    [HttpGet("{id}")]
    public TestArticle Get(string id) => new() { Id = id, Title = "Hello" };

    [HttpPost]
    public TestArticle Post([FromBody] TestArticle article) => article;
}

public class TestProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers().AddJsonApi(options => options.UseAttributes());

        var app = builder.Build();
        app.MapControllers();
        app.Run();
    }
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<TestProgram>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }
}
