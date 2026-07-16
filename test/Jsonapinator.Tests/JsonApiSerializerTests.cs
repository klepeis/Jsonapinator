using System.Collections;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

        [JsonApiMeta]
        public MetaObject? ResourceMeta { get; set; }

        [JsonApiLinks]
        public LinksObject? ResourceLinks { get; set; }

        [JsonApiRelationshipMeta("author")]
        public MetaObject? AuthorMeta { get; set; }

        [JsonApiRelationshipLinks("author")]
        public LinksObject? AuthorLinks { get; set; }
    }

    private sealed class ConventionArticle
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        public ConventionPerson? Author { get; set; }

        public MetaObject? Meta { get; set; }

        public LinksObject? Links { get; set; }

        public MetaObject? AuthorMeta { get; set; }

        public LinksObject? AuthorLinks { get; set; }
    }

    private sealed class ConventionPerson
    {
        public string Id { get; set; } = "";
    }

    [JsonApiResource("media")]
    private sealed class Media
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiType]
        public string? MediaType { get; set; }
    }

    private sealed class ConventionMedia
    {
        public string Id { get; set; } = "";

        public string? Type { get; set; }
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(Circle), "circle")]
    [JsonDerivedType(typeof(Square), "square")]
    private abstract class Shape
    {
    }

    private sealed class Circle : Shape
    {
        public double Radius { get; set; }
    }

    private sealed class Square : Shape
    {
        public double Side { get; set; }
    }

    [JsonApiResource("shape-holders")]
    private sealed class ShapeHolder
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public Shape? FeaturedShape { get; set; }
    }

    private sealed class ConventionShapeHolder
    {
        public string Id { get; set; } = "";

        public Shape? FeaturedShape { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Video), "videos")]
    [JsonDerivedType(typeof(Image), "images")]
    private abstract class Attachment
    {
        [JsonApiId]
        public string Id { get; set; } = "";
    }

    [JsonApiResource("videos")]
    private sealed class Video : Attachment
    {
        [JsonApiAttribute]
        public int DurationSeconds { get; set; }
    }

    [JsonApiResource("images")]
    private sealed class Image : Attachment
    {
    }

    [JsonApiResource("gallery-articles")]
    private sealed class GalleryArticle
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
        public List<Attachment> Attachments { get; set; } = new();
    }

    // Convention mode derives the JSON:API "type" from the camelCase class name (see
    // ConventionResourceTypeResolver), so these subtype names ("Videos"/"Images") are chosen to
    // match the [JsonDerivedType] discriminator strings ("videos"/"images") exactly, so the
    // round trip is consistent between the two.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Videos), "videos")]
    [JsonDerivedType(typeof(Images), "images")]
    private abstract class ConventionAttachment
    {
        public string Id { get; set; } = "";
    }

    private sealed class Videos : ConventionAttachment
    {
        public int DurationSeconds { get; set; }
    }

    private sealed class Images : ConventionAttachment
    {
    }

    private sealed class ConventionGalleryArticle
    {
        public string Id { get; set; } = "";

        public List<ConventionAttachment> Attachments { get; set; } = new();
    }

    [JsonApiResource("people")]
    private sealed class Person
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string FirstName { get; set; } = "";
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
    public void Serialize_object_overload_produces_a_valid_single_resource_document()
    {
        object article = new Article { Id = "1", Title = "Bikeshedding" };

        var json = _serializer.Serialize(article);

        var node = JsonNode.Parse(json)!;
        Assert.Equal("articles", node["data"]!["type"]!.GetValue<string>());
        Assert.Equal("Bikeshedding", node["data"]!["attributes"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void SerializeCollection_ienumerable_overload_produces_a_data_array()
    {
        IEnumerable articles = new List<Article> { new() { Id = "1" }, new() { Id = "2" } };

        var json = _serializer.SerializeCollection(articles);

        var node = JsonNode.Parse(json)!;
        Assert.Equal(2, node["data"]!.AsArray().Count);
    }

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
    public void Serialize_with_Include_option_produces_an_included_array()
    {
        var article = new Article { Id = "1", Author = new Person { Id = "9", FirstName = "Dan" } };

        var json = _serializer.Serialize(article, new JsonApiDocumentOptions { Include = new[] { "author" } });

        var node = JsonNode.Parse(json)!;
        var included = node["included"]!.AsArray();
        Assert.Single(included);
        Assert.Equal("Dan", included[0]!["attributes"]!["firstName"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_without_Include_option_has_no_included_member()
    {
        var article = new Article { Id = "1", Author = new Person { Id = "9", FirstName = "Dan" } };

        var json = _serializer.Serialize(article);

        var node = JsonNode.Parse(json)!;
        Assert.False(node.AsObject().ContainsKey("included"));
    }

    [Fact]
    public void SerializeCollection_with_Include_option_produces_an_included_array()
    {
        var articles = new List<Article>
        {
            new() { Id = "1", Author = new Person { Id = "9", FirstName = "Dan" } },
            new() { Id = "2", Author = new Person { Id = "10", FirstName = "Sam" } },
        };

        var json = _serializer.SerializeCollection(articles, new JsonApiDocumentOptions { Include = new[] { "author" } });

        var node = JsonNode.Parse(json)!;
        Assert.Equal(2, node["included"]!.AsArray().Count);
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
    public void Deserialize_hydrates_relationships_from_the_included_array()
    {
        var json = """
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}},
             "included":[{"type":"people","id":"9","attributes":{"firstName":"Dan"}}]}
            """;

        var article = _serializer.Deserialize<Article>(json);

        Assert.Equal("Dan", article.Author!.FirstName);
    }

    [Fact]
    public void DeserializeCollection_hydrates_relationships_shared_across_primary_resources_from_included()
    {
        var json = """
            {"data":[
                {"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}},
                {"type":"articles","id":"2","relationships":{"author":{"data":{"type":"people","id":"9"}}}}
             ],
             "included":[{"type":"people","id":"9","attributes":{"firstName":"Dan"}}]}
            """;

        var articles = _serializer.DeserializeCollection<Article>(json);

        Assert.Equal("Dan", articles[0].Author!.FirstName);
        Assert.Equal("Dan", articles[1].Author!.FirstName);
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
    public void Deserialize_with_runtime_Type_maps_a_single_resource_document()
    {
        var article = (Article)_serializer.Deserialize(typeof(Article), """
            {"data":{"type":"articles","id":"1","attributes":{"title":"Hello"}}}
            """);

        Assert.Equal("1", article.Id);
        Assert.Equal("Hello", article.Title);
    }

    [Fact]
    public void DeserializeCollection_with_runtime_Type_maps_a_resource_collection_document()
    {
        var list = _serializer.DeserializeCollection(typeof(Article), """
            {"data":[{"type":"articles","id":"1"},{"type":"articles","id":"2"}]}
            """);

        Assert.Equal(2, list.Count);
        Assert.Equal("2", ((Article)list[1]!).Id);
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
    public void Serialize_and_Deserialize_round_trip_resource_and_relationship_level_meta_and_links()
    {
        var original = new Article
        {
            Id = "1",
            Title = "Round Trip",
            Author = new Person { Id = "9" },
            ResourceMeta = new MetaObject { ["views"] = 42 },
            ResourceLinks = new LinksObject { ["self"] = "/articles/1" },
            AuthorMeta = new MetaObject { ["role"] = "editor" },
            AuthorLinks = new LinksObject { ["self"] = "/articles/1/relationships/author" },
        };

        var json = _serializer.Serialize(original);
        var roundTripped = _serializer.Deserialize<Article>(json);

        Assert.Equal(42, ((JsonNode)roundTripped.ResourceMeta!["views"]!).GetValue<int>());
        Assert.Equal("/articles/1", roundTripped.ResourceLinks!["self"]);
        Assert.Equal("editor", ((JsonNode)roundTripped.AuthorMeta!["role"]!).GetValue<string>());
        Assert.Equal("/articles/1/relationships/author", roundTripped.AuthorLinks!["self"]);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_resource_and_relationship_level_meta_and_links_with_conventions()
    {
        var conventionSerializer = JsonApiSerializer.WithConventions();
        var original = new ConventionArticle
        {
            Id = "1",
            Title = "Round Trip",
            Author = new ConventionPerson { Id = "9" },
            Meta = new MetaObject { ["views"] = 42 },
            Links = new LinksObject { ["self"] = "/articles/1" },
            AuthorMeta = new MetaObject { ["role"] = "editor" },
            AuthorLinks = new LinksObject { ["self"] = "/articles/1/relationships/author" },
        };

        var json = conventionSerializer.Serialize(original);
        var roundTripped = conventionSerializer.Deserialize<ConventionArticle>(json);

        Assert.Equal(42, ((JsonNode)roundTripped.Meta!["views"]!).GetValue<int>());
        Assert.Equal("/articles/1", roundTripped.Links!["self"]);
        Assert.Equal("editor", ((JsonNode)roundTripped.AuthorMeta!["role"]!).GetValue<string>());
        Assert.Equal("/articles/1/relationships/author", roundTripped.AuthorLinks!["self"]);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_the_type_override_property()
    {
        var video = new Media { Id = "1", MediaType = "videos" };

        var json = _serializer.Serialize(video);
        var roundTripped = _serializer.Deserialize<Media>(json);

        var node = JsonNode.Parse(json)!;
        Assert.Equal("videos", node["data"]!["type"]!.GetValue<string>());
        Assert.Equal("videos", roundTripped.MediaType);
    }

    [Fact]
    public void Serialize_falls_back_to_the_declared_resource_type_when_the_override_is_null()
    {
        var media = new Media { Id = "1", MediaType = null };

        var json = _serializer.Serialize(media);

        var node = JsonNode.Parse(json)!;
        Assert.Equal("media", node["data"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_the_type_override_property_with_conventions()
    {
        var conventionSerializer = JsonApiSerializer.WithConventions();
        var video = new ConventionMedia { Id = "1", Type = "videos" };

        var json = conventionSerializer.Serialize(video);
        var roundTripped = conventionSerializer.Deserialize<ConventionMedia>(json);

        var node = JsonNode.Parse(json)!;
        Assert.Equal("videos", node["data"]!["type"]!.GetValue<string>());
        Assert.Equal("videos", roundTripped.Type);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_a_single_valued_polymorphic_attribute()
    {
        var holder = new ShapeHolder { Id = "1", FeaturedShape = new Circle { Radius = 5 } };

        var json = _serializer.Serialize(holder);
        var roundTripped = _serializer.Deserialize<ShapeHolder>(json);

        var circle = Assert.IsType<Circle>(roundTripped.FeaturedShape);
        Assert.Equal(5, circle.Radius);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_a_single_valued_polymorphic_attribute_with_conventions()
    {
        var conventionSerializer = JsonApiSerializer.WithConventions();
        var holder = new ConventionShapeHolder { Id = "1", FeaturedShape = new Square { Side = 3 } };

        var json = conventionSerializer.Serialize(holder);
        var roundTripped = conventionSerializer.Deserialize<ConventionShapeHolder>(json);

        var square = Assert.IsType<Square>(roundTripped.FeaturedShape);
        Assert.Equal(3, square.Side);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_a_polymorphic_to_many_relationship()
    {
        // Relationships round-trip as identifier-only stubs unless a matching entry is present in
        // "included" (same as any other relationship, see compound-documents.md) — so only the
        // resolved CLR subtype and id are checked here, not Video's own DurationSeconds attribute.
        var article = new GalleryArticle
        {
            Id = "1",
            Attachments = new List<Attachment>
            {
                new Video { Id = "1", DurationSeconds = 42 },
                new Image { Id = "2" },
            },
        };

        var json = _serializer.Serialize(article);
        var roundTripped = _serializer.Deserialize<GalleryArticle>(json);

        var video = Assert.IsType<Video>(roundTripped.Attachments[0]);
        Assert.Equal("1", video.Id);
        Assert.IsType<Image>(roundTripped.Attachments[1]);
        Assert.Equal("2", roundTripped.Attachments[1].Id);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_a_polymorphic_to_many_relationship_with_conventions()
    {
        var conventionSerializer = JsonApiSerializer.WithConventions();
        var article = new ConventionGalleryArticle
        {
            Id = "1",
            Attachments = new List<ConventionAttachment>
            {
                new Videos { Id = "1", DurationSeconds = 42 },
                new Images { Id = "2" },
            },
        };

        var json = conventionSerializer.Serialize(article);
        var roundTripped = conventionSerializer.Deserialize<ConventionGalleryArticle>(json);

        var video = Assert.IsType<Videos>(roundTripped.Attachments[0]);
        Assert.Equal("1", video.Id);
        Assert.IsType<Images>(roundTripped.Attachments[1]);
        Assert.Equal("2", roundTripped.Attachments[1].Id);
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
