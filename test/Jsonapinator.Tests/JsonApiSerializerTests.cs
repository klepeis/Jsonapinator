using System.Text.Json.Nodes;
using Jsonapinator;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Exceptions;

namespace Jsonapinator.Tests;

public class JsonApiSerializerTests
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
    }

    [JsonApiResource("people")]
    private sealed class Person
    {
        [JsonApiId]
        public string Id { get; set; } = "";
    }

    [JsonApiResource("bad-id")]
    private sealed class GuidResource
    {
        [JsonApiId]
        public Guid Id { get; set; }
    }

    private sealed class NotAResource
    {
        public string Id { get; set; } = "";
    }

    private readonly JsonApiSerializer _serializer = new();

    [Fact]
    public void Serialize_produces_a_valid_single_resource_document()
    {
        var json = _serializer.Serialize(new Article { Id = "1", Title = "Bikeshedding" });

        var node = JsonNode.Parse(json)!;
        Assert.Equal("articles", node["data"]!["type"]!.GetValue<string>());
        Assert.Equal("Bikeshedding", node["data"]!["attributes"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_collection_produces_a_data_array()
    {
        var articles = new List<Article> { new() { Id = "1" }, new() { Id = "2" } };

        var json = _serializer.SerializeCollection(articles);

        var node = JsonNode.Parse(json)!;
        Assert.Equal(2, node["data"]!.AsArray().Count);
    }

    [Fact]
    public void Serialize_applies_document_level_options()
    {
        var options = new JsonApiDocumentOptions
        {
            Meta = new MetaObject { ["copyright"] = "Copyright 2026" },
            Links = new LinksObject { ["self"] = "http://example.com/articles/1" },
        };

        var json = _serializer.Serialize(new Article { Id = "1" }, options);

        var node = JsonNode.Parse(json)!;
        Assert.Equal("Copyright 2026", node["meta"]!["copyright"]!.GetValue<string>());
        Assert.Equal("http://example.com/articles/1", node["links"]!["self"]!.GetValue<string>());
    }

    [Fact]
    public void SerializeErrors_produces_an_errors_document()
    {
        var json = _serializer.SerializeErrors(new[] { new ErrorObject { Status = "422", Title = "Invalid" } });

        var node = JsonNode.Parse(json)!;
        Assert.False(node.AsObject().ContainsKey("data"));
        Assert.Equal("422", node["errors"]![0]!["status"]!.GetValue<string>());
    }

    [Fact]
    public void Deserialize_maps_a_single_resource_document_to_a_poco()
    {
        var article = _serializer.Deserialize<Article>("""
            {"data":{"type":"articles","id":"1","attributes":{"title":"Hello"},"relationships":{"author":{"data":{"type":"people","id":"9"}}}}}
            """);

        Assert.Equal("1", article.Id);
        Assert.Equal("Hello", article.Title);
        Assert.Equal("9", article.Author!.Id);
    }

    [Fact]
    public void DeserializeCollection_maps_a_resource_collection_document()
    {
        var articles = _serializer.DeserializeCollection<Article>("""
            {"data":[{"type":"articles","id":"1"},{"type":"articles","id":"2"}]}
            """);

        Assert.Equal(2, articles.Count);
        Assert.Equal("2", articles[1].Id);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_a_resource()
    {
        var original = new Article { Id = "1", Title = "Round Trip", Author = new Person { Id = "9" } };

        var json = _serializer.Serialize(original);
        var roundTripped = _serializer.Deserialize<Article>(json);

        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.Title, roundTripped.Title);
        Assert.Equal(original.Author.Id, roundTripped.Author!.Id);
    }

    [Fact]
    public void BuildDocument_returns_the_document_model_without_serializing_to_json()
    {
        var document = _serializer.BuildDocument(new Article { Id = "1" });

        Assert.Equal("articles", document.Data!.Single!.Type);
    }

    [Fact]
    public void ParseDocument_returns_the_document_model_without_mapping_to_a_poco()
    {
        var document = _serializer.ParseDocument("""{"data":{"type":"articles","id":"1"}}""");

        Assert.Equal("articles", document.Data!.Single!.Type);
    }

    [Fact]
    public void Serialize_throws_a_mapping_exception_when_the_type_is_not_a_resource()
    {
        Assert.Throws<JsonApiMappingException>(() => _serializer.Serialize(new NotAResource()));
    }

    [Fact]
    public void Deserialize_throws_a_mapping_exception_for_malformed_json()
    {
        Assert.Throws<JsonApiMappingException>(() => _serializer.Deserialize<Article>("not json"));
    }

    [Fact]
    public void Deserialize_throws_a_mapping_exception_for_an_id_type_mismatch()
    {
        Assert.Throws<JsonApiMappingException>(() =>
            _serializer.Deserialize<GuidResource>("""{"data":{"type":"bad-id","id":"not-a-guid"}}"""));
    }
}
