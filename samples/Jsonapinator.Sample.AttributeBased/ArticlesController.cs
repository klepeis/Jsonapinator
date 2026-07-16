using Jsonapinator;
using Jsonapinator.AspNetCore.Formatters;
using Microsoft.AspNetCore.Mvc;

namespace Jsonapinator.Sample.AttributeBased;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    private static readonly Person Dan = new() { Id = "9", FirstName = "Dan", LastName = "Gebhardt" };

    private static readonly List<Article> Articles =
    [
        new Article
        {
            Id = "1",
            Title = "JSON:API paints my bikeshed!",
            PublishedAtUtc = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Author = Dan,
            Comments =
            [
                new Comment { Id = "5", Body = "First!", Author = Dan },
                new Comment { Id = "12", Body = "I like XML better", Author = new Person { Id = "2", FirstName = "Kat" } },
            ],
            Tags = ["json-api", "rest"],
            Seo = new ArticleSeo { MetaTitle = "JSON:API paints my bikeshed!", MetaDescription = "A tour of JSON:API." },
            InternalNotes = "Never appears in the JSON:API output.",
        },
    ];

    // The JsonApiSerializer registered by AddJsonApi() is injected here via DI (a singleton),
    // used only by the with-includes action below — see its comment for why.
    private readonly JsonApiSerializer _serializer;

    public ArticlesController(JsonApiSerializer serializer)
    {
        _serializer = serializer;
    }

    [HttpGet]
    public IEnumerable<Article> GetAll() => Articles;

    [HttpGet("{id}")]
    public ActionResult<Article> GetById(string id)
    {
        var article = Articles.FirstOrDefault(a => a.Id == id);
        return article is null ? NotFound() : article;
    }

    // JsonApiOutputFormatter always calls JsonApiSerializer.Serialize(object) with no
    // JsonApiDocumentOptions, so "include" can't be triggered through a normal
    // controller-returns-POCO action today (see _docs/future-roadmap.md). This action shows the
    // workaround: call the serializer directly and return the JSON as Content.
    [HttpGet("{id}/with-includes")]
    public IActionResult GetByIdWithIncludes(string id)
    {
        var article = Articles.FirstOrDefault(a => a.Id == id);
        if (article is null)
        {
            return NotFound();
        }

        var options = new JsonApiDocumentOptions { Include = new[] { "author", "comments.author" } };
        var json = _serializer.Serialize(article, options);
        return Content(json, JsonApiOutputFormatter.MediaType);
    }

    [HttpPost]
    public ActionResult<Article> Create([FromBody] Article article)
    {
        Articles.Add(article);
        return CreatedAtAction(nameof(GetById), new { id = article.Id }, article);
    }
}
