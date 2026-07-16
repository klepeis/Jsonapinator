using System.Text.Json.Nodes;
using Jsonapinator;
using Jsonapinator.Attributes;

namespace Jsonapinator.Tests;

/// <summary>
/// Round-trips the canonical "articles with author and comments" example from
/// https://jsonapi.org/format/#document-top-level to verify the serialized shape matches the
/// spec structurally, and that a deserialize-then-serialize cycle is stable.
/// </summary>
public class SpecExampleRoundTripTests
{
    [JsonApiResource("articles")]
    private sealed class Article
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "";

        [JsonApiRelationship("author", RelationshipKind.ToOne)]
        public Person? Author { get; set; }

        [JsonApiRelationship("comments", RelationshipKind.ToMany)]
        public List<Comment> Comments { get; set; } = new();
    }

    [JsonApiResource("people")]
    private sealed class Person
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string FirstName { get; set; } = "";

        [JsonApiAttribute]
        public string LastName { get; set; } = "";

        [JsonApiAttribute]
        public string Twitter { get; set; } = "";
    }

    [JsonApiResource("comments")]
    private sealed class Comment
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Body { get; set; } = "";

        [JsonApiRelationship("author", RelationshipKind.ToOne)]
        public Person? Author { get; set; }
    }

    private static Article BuildSpecExampleArticle()
    {
        var author = new Person { Id = "9", FirstName = "Dan", LastName = "Gebhardt", Twitter = "dgeb" };
        return new Article
        {
            Id = "1",
            Title = "JSON:API paints my bikeshed!",
            Author = author,
            Comments = new List<Comment>
            {
                new() { Id = "5", Body = "First!", Author = author },
                new() { Id = "12", Body = "I like XML better", Author = new Person { Id = "2", FirstName = "Kat" } },
            },
        };
    }

    [Fact]
    public void Serialized_document_matches_the_spec_structure_field_for_field()
    {
        var serializer = new JsonApiSerializer();

        var json = serializer.Serialize(BuildSpecExampleArticle());
        var node = JsonNode.Parse(json)!;

        var data = node["data"]!;
        Assert.Equal("articles", data["type"]!.GetValue<string>());
        Assert.Equal("1", data["id"]!.GetValue<string>());
        Assert.Equal("JSON:API paints my bikeshed!", data["attributes"]!["title"]!.GetValue<string>());

        var author = data["relationships"]!["author"]!["data"]!;
        Assert.Equal("people", author["type"]!.GetValue<string>());
        Assert.Equal("9", author["id"]!.GetValue<string>());

        var comments = data["relationships"]!["comments"]!["data"]!.AsArray();
        Assert.Equal(2, comments.Count);
        Assert.Equal("comments", comments[0]!["type"]!.GetValue<string>());
        Assert.Equal("5", comments[0]!["id"]!.GetValue<string>());
        Assert.Equal("12", comments[1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Deserialize_then_serialize_reproduces_equivalent_state()
    {
        var serializer = new JsonApiSerializer();
        var original = BuildSpecExampleArticle();

        var firstPass = serializer.Serialize(original);
        var roundTripped = serializer.Deserialize<Article>(firstPass);
        var secondPass = serializer.Serialize(roundTripped);

        Assert.Equal(
            JsonNode.Parse(firstPass)!.ToJsonString(),
            JsonNode.Parse(secondPass)!.ToJsonString());
    }

    [Fact]
    public void Serialize_with_include_then_deserialize_hydrates_full_related_resource_attributes()
    {
        var serializer = new JsonApiSerializer();
        var options = new JsonApiDocumentOptions { Include = new[] { "author", "comments.author" } };

        var json = serializer.Serialize(BuildSpecExampleArticle(), options);
        var roundTripped = serializer.Deserialize<Article>(json);

        Assert.Equal("Dan", roundTripped.Author!.FirstName);
        Assert.Equal("Dan", roundTripped.Comments[0].Author!.FirstName);
        Assert.Equal("Kat", roundTripped.Comments[1].Author!.FirstName);
    }

    [Fact]
    public void Included_array_dedups_the_shared_author_reachable_via_two_paths()
    {
        var serializer = new JsonApiSerializer();
        var options = new JsonApiDocumentOptions { Include = new[] { "author", "comments.author" } };

        var document = serializer.BuildDocument(BuildSpecExampleArticle(), options.Include);

        Assert.Single(document.Included!, r => r.Type == "people" && r.Id == "9");
    }

    [Fact]
    public void Deserialized_article_preserves_attribute_and_relationship_values()
    {
        var serializer = new JsonApiSerializer();
        var json = serializer.Serialize(BuildSpecExampleArticle());

        var article = serializer.Deserialize<Article>(json);

        Assert.Equal("JSON:API paints my bikeshed!", article.Title);
        Assert.Equal("9", article.Author!.Id);
        Assert.Equal(2, article.Comments.Count);
    }
}
