using Microsoft.AspNetCore.Mvc;

namespace Jsonapinator.Sample.ErrorHandling.Default;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    [HttpPost]
    public Article Post([FromBody] Article article) => article;

    [HttpGet("boom")]
    public Article GetBoom() => throw new InvalidOperationException("some sensitive internal detail");
}
