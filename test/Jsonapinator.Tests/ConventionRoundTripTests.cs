using System.Text.Json.Nodes;

namespace Jsonapinator.Tests;

/// <summary>
/// End-to-end proof that <see cref="JsonApiSerializer.WithConventions"/> round-trips a plain
/// POCO graph with zero <c>Jsonapinator.Attributes</c> anywhere on it.
/// </summary>
public class ConventionRoundTripTests
{
    private sealed class Person
    {
        public string Id { get; set; } = "";

        public string FirstName { get; set; } = "";
    }

    private sealed class Comment
    {
        public string Id { get; set; } = "";

        public string Body { get; set; } = "";
    }

    private sealed class Article
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        public Person? Author { get; set; }

        public List<Comment> Comments { get; set; } = new();
    }

    [Fact]
    public void Serialize_produces_the_expected_shape_with_no_attributes_on_the_poco()
    {
        var article = new Article
        {
            Id = "1",
            Title = "JSON:API paints my bikeshed!",
            Author = new Person { Id = "9", FirstName = "Dan" },
            Comments = new List<Comment> { new() { Id = "5", Body = "First!" } },
        };

        var json = JsonApiSerializer.WithConventions().Serialize(article);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("article", node["data"]!["type"]!.GetValue<string>());
        Assert.Equal("1", node["data"]!["id"]!.GetValue<string>());
        Assert.Equal("JSON:API paints my bikeshed!", node["data"]!["attributes"]!["title"]!.GetValue<string>());

        var author = node["data"]!["relationships"]!["author"]!["data"]!;
        Assert.Equal("person", author["type"]!.GetValue<string>());
        Assert.Equal("9", author["id"]!.GetValue<string>());

        var comments = node["data"]!["relationships"]!["comments"]!["data"]!.AsArray();
        Assert.Equal("comment", comments[0]!["type"]!.GetValue<string>());
        Assert.Equal("5", comments[0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_then_deserialize_round_trips_a_plain_poco_graph()
    {
        var serializer = JsonApiSerializer.WithConventions();
        var original = new Article
        {
            Id = "1",
            Title = "Round Trip",
            Author = new Person { Id = "9", FirstName = "Dan" },
            Comments = new List<Comment> { new() { Id = "5", Body = "First!" } },
        };

        var json = serializer.Serialize(original);
        var roundTripped = serializer.Deserialize<Article>(json);

        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.Title, roundTripped.Title);
        Assert.Equal(original.Author.Id, roundTripped.Author!.Id);
        Assert.Equal(original.Comments[0].Id, roundTripped.Comments[0].Id);
    }
}
