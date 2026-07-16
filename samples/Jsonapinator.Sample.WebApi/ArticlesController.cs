using Microsoft.AspNetCore.Mvc;

namespace Jsonapinator.Sample.WebApi;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    private static readonly List<Article> Articles =
    [
        new Article { Id = "1", Title = "JSON:API paints my bikeshed!" },
    ];

    [HttpGet]
    public IEnumerable<Article> GetAll() => Articles;

    [HttpGet("{id}")]
    public ActionResult<Article> GetById(string id)
    {
        var article = Articles.FirstOrDefault(a => a.Id == id);
        return article is null ? NotFound() : article;
    }

    [HttpPost]
    public ActionResult<Article> Create([FromBody] Article article)
    {
        Articles.Add(article);
        return CreatedAtAction(nameof(GetById), new { id = article.Id }, article);
    }
}
