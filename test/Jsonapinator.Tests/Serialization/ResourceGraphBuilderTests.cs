using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Metadata;
using Jsonapinator.Serialization;

namespace Jsonapinator.Tests.Serialization;

public class ResourceGraphBuilderTests
{
    [JsonApiResource("articles")]
    private sealed class Article
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "";

        [JsonApiAttribute]
        [JsonPropertyName("word-count")]
        public int WordCount { get; set; }

        [JsonApiRelationship("author", RelationshipKind.ToOne)]
        public Person? Author { get; set; }

        [JsonApiRelationship("comments", RelationshipKind.ToMany)]
        public List<Comment> Comments { get; set; } = new();

        [JsonApiMeta]
        public MetaObject? ResourceMeta { get; set; }

        [JsonApiLinks]
        public LinksObject? ResourceLinks { get; set; }

        [JsonApiRelationshipMeta("comments")]
        public MetaObject? CommentsMeta { get; set; }

        [JsonApiRelationshipLinks("comments")]
        public LinksObject? CommentsLinks { get; set; }
    }

    [JsonApiResource("people")]
    private sealed class Person
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string FirstName { get; set; } = "";
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

    [JsonApiResource("chain-nodes")]
    private sealed class ChainNode
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("next", RelationshipKind.ToOne)]
        public ChainNode? Next { get; set; }
    }

    [JsonApiResource("media")]
    private sealed class Media
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiType]
        public string? MediaType { get; set; }
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

        [JsonApiAttribute]
        public List<Shape> Shapes { get; set; } = new();
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
    }

    [JsonApiResource("images")]
    private sealed class Image : Attachment
    {
    }

    [JsonApiResource("attachment-holders")]
    private sealed class AttachmentHolder
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
        public List<Attachment> Attachments { get; set; } = new();
    }

    [JsonApiResource("guid-things")]
    private sealed class GuidThing
    {
        [JsonApiId]
        public Guid Id { get; set; }
    }

    [JsonApiResource("int-things")]
    private sealed class IntThing
    {
        [JsonApiId]
        public int Id { get; set; }
    }

    [JsonApiResource("long-things")]
    private sealed class LongThing
    {
        [JsonApiId]
        public long Id { get; set; }
    }

    private readonly ResourceGraphBuilder _builder = new(new ResourceTypeResolver());

    [Fact]
    public void BuildResource_maps_type_and_id()
    {
        var article = new Article { Id = "1", Title = "T", WordCount = 5 };

        var resource = _builder.BuildResource(article);

        Assert.Equal("articles", resource.Type);
        Assert.Equal("1", resource.Id);
    }

    [Fact]
    public void BuildResource_maps_attributes_using_resolved_json_names()
    {
        var article = new Article { Id = "1", Title = "Bikeshedding", WordCount = 42 };

        var resource = _builder.BuildResource(article);

        Assert.Equal("Bikeshedding", resource.Attributes!["title"]);
        Assert.Equal(42, resource.Attributes["word-count"]);
    }

    [Theory]
    [MemberData(nameof(IdConversionCases))]
    public void BuildResource_serializes_all_supported_id_types_as_strings(object resource, string expectedId)
    {
        var builder = new ResourceGraphBuilder(new ResourceTypeResolver());
        var built = builder.BuildResource(resource);

        Assert.Equal(expectedId, built.Id);
    }

    public static IEnumerable<object[]> IdConversionCases()
    {
        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        yield return new object[] { new GuidThing { Id = guid }, guid.ToString() };
        yield return new object[] { new IntThing { Id = 7 }, "7" };
        yield return new object[] { new LongThing { Id = 12345678900L }, "12345678900" };
    }

    [Fact]
    public void BuildResource_maps_a_non_null_to_one_relationship()
    {
        var article = new Article { Id = "1", Author = new Person { Id = "9" } };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["author"];
        Assert.False(relationship.IsToMany);
        Assert.Equal("people", relationship.SingleData!.Type);
        Assert.Equal("9", relationship.SingleData.Id);
    }

    [Fact]
    public void BuildResource_maps_a_null_to_one_relationship()
    {
        var article = new Article { Id = "1", Author = null };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["author"];
        Assert.False(relationship.IsToMany);
        Assert.Null(relationship.SingleData);
    }

    [Fact]
    public void BuildResource_maps_a_populated_to_many_relationship()
    {
        var article = new Article
        {
            Id = "1",
            Comments = new List<Comment> { new() { Id = "5" }, new() { Id = "12" } },
        };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["comments"];
        Assert.True(relationship.IsToMany);
        Assert.Equal(2, relationship.ManyData!.Count);
        Assert.Equal("5", relationship.ManyData[0].Id);
    }

    [Fact]
    public void BuildResource_maps_an_empty_to_many_relationship()
    {
        var article = new Article { Id = "1", Comments = new List<Comment>() };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["comments"];
        Assert.True(relationship.IsToMany);
        Assert.Empty(relationship.ManyData!);
    }

    [Fact]
    public void BuildDocument_wraps_a_single_resource()
    {
        var document = _builder.BuildDocument(new Article { Id = "1" });

        Assert.False(document.Data!.IsCollection);
        Assert.Equal("1", document.Data.Single!.Id);
    }

    [Fact]
    public void BuildCollectionDocument_wraps_a_resource_collection()
    {
        var articles = new List<Article> { new() { Id = "1" }, new() { Id = "2" } };

        var document = _builder.BuildCollectionDocument(articles);

        Assert.True(document.Data!.IsCollection);
        Assert.Equal(2, document.Data.Collection!.Count);
    }

    [Fact]
    public void BuildCollectionDocument_wraps_an_empty_collection()
    {
        var document = _builder.BuildCollectionDocument(Enumerable.Empty<Article>());

        Assert.True(document.Data!.IsCollection);
        Assert.Empty(document.Data.Collection!);
    }

    [Fact]
    public void BuildCollectionDocument_with_include_paths_collects_included_resources_across_all_items()
    {
        var articles = new List<Article>
        {
            new() { Id = "1", Author = new Person { Id = "9", FirstName = "Dan" } },
            new() { Id = "2", Author = new Person { Id = "10", FirstName = "Sam" } },
        };

        var document = _builder.BuildCollectionDocument(articles, new[] { "author" });

        Assert.Equal(2, document.Included!.Count);
    }

    [Fact]
    public void BuildDocument_with_include_paths_includes_the_related_resources_full_attributes()
    {
        var article = new Article { Id = "1", Author = new Person { Id = "9", FirstName = "Dan" } };

        var document = _builder.BuildDocument(article, new[] { "author" });

        var included = Assert.Single(document.Included!);
        Assert.Equal("people", included.Type);
        Assert.Equal("9", included.Id);
        Assert.Equal("Dan", included.Attributes!["firstName"]);
    }

    [Fact]
    public void BuildDocument_with_no_include_paths_leaves_included_null()
    {
        var document = _builder.BuildDocument(new Article { Id = "1", Author = new Person { Id = "9" } });

        Assert.Null(document.Included);
    }

    [Fact]
    public void BuildDocument_with_a_multi_level_include_path_includes_intermediate_and_leaf_resources()
    {
        var article = new Article
        {
            Id = "1",
            Comments = new List<Comment>
            {
                new() { Id = "5", Author = new Person { Id = "9", FirstName = "Dan" } },
            },
        };

        var document = _builder.BuildDocument(article, new[] { "comments.author" });

        Assert.Equal(2, document.Included!.Count);
        Assert.Contains(document.Included, r => r.Type == "comments" && r.Id == "5");
        Assert.Contains(document.Included, r => r.Type == "people" && r.Id == "9");
    }

    [Fact]
    public void BuildDocument_with_a_direct_to_many_include_path_includes_each_element()
    {
        var article = new Article
        {
            Id = "1",
            Comments = new List<Comment> { new() { Id = "5" }, new() { Id = "12" } },
        };

        var document = _builder.BuildDocument(article, new[] { "comments" });

        Assert.Equal(2, document.Included!.Count);
    }

    [Fact]
    public void BuildDocument_dedups_a_resource_reachable_via_two_include_paths()
    {
        var author = new Person { Id = "9", FirstName = "Dan" };
        var article = new Article
        {
            Id = "1",
            Author = author,
            Comments = new List<Comment> { new() { Id = "5", Author = author } },
        };

        var document = _builder.BuildDocument(article, new[] { "author", "comments.author" });

        Assert.Single(document.Included!, r => r.Type == "people" && r.Id == "9");
    }

    [Fact]
    public void BuildDocument_never_duplicates_the_primary_resource_into_included()
    {
        var article = new Article { Id = "1" };
        article.Comments = new List<Comment> { new() { Id = "5" } };

        var document = _builder.BuildDocument(article, new[] { "comments" });

        Assert.DoesNotContain(document.Included!, r => r.Type == "articles" && r.Id == "1");
    }

    [Fact]
    public void BuildResource_maps_resource_level_meta_and_links()
    {
        var article = new Article
        {
            Id = "1",
            ResourceMeta = new MetaObject { ["views"] = 42 },
            ResourceLinks = new LinksObject { ["self"] = "/articles/1" },
        };

        var resource = _builder.BuildResource(article);

        Assert.Equal(42, resource.Meta!["views"]);
        Assert.Equal("/articles/1", resource.Links!["self"]);
    }

    [Fact]
    public void BuildResource_leaves_resource_level_meta_and_links_null_when_not_set()
    {
        var article = new Article { Id = "1" };

        var resource = _builder.BuildResource(article);

        Assert.Null(resource.Meta);
        Assert.Null(resource.Links);
    }

    [Fact]
    public void BuildResource_maps_relationship_level_meta_and_links()
    {
        var article = new Article
        {
            Id = "1",
            Comments = new List<Comment> { new() { Id = "5" } },
            CommentsMeta = new MetaObject { ["count"] = 1 },
            CommentsLinks = new LinksObject { ["self"] = "/articles/1/relationships/comments" },
        };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["comments"];
        Assert.Equal(1, relationship.Meta!["count"]);
        Assert.Equal("/articles/1/relationships/comments", relationship.Links!["self"]);
    }

    [Fact]
    public void BuildResource_leaves_relationship_level_meta_and_links_null_for_a_relationship_without_them()
    {
        var article = new Article { Id = "1", Author = new Person { Id = "9" } };

        var resource = _builder.BuildResource(article);

        var relationship = resource.Relationships!["author"];
        Assert.Null(relationship.Meta);
        Assert.Null(relationship.Links);
    }

    [Fact]
    public void BuildDocument_throws_on_an_unknown_include_path_segment()
    {
        var article = new Article { Id = "1" };

        var ex = Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => _builder.BuildDocument(article, new[] { "publisher" }));

        Assert.Contains("publisher", ex.Message);
        Assert.Contains("articles", ex.Message);
    }

    private static ChainNode BuildChain(int count)
    {
        var head = new ChainNode { Id = "1" };
        var current = head;
        for (var i = 2; i <= count; i++)
        {
            var next = new ChainNode { Id = i.ToString() };
            current.Next = next;
            current = next;
        }

        return head;
    }

    private static string[] IncludePath(int hops) =>
        new[] { string.Join(".", Enumerable.Repeat("next", hops)) };

    [Fact]
    public void BuildDocument_throws_when_an_include_path_walk_exceeds_MaxIncludeDepth()
    {
        var builder = new ResourceGraphBuilder(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludeDepth = 3 });
        var chain = BuildChain(10);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => builder.BuildDocument(chain, IncludePath(9)));
    }

    [Fact]
    public void BuildDocument_succeeds_when_an_include_path_walk_is_within_MaxIncludeDepth()
    {
        var builder = new ResourceGraphBuilder(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludeDepth = 20 });
        var chain = BuildChain(6);

        var document = builder.BuildDocument(chain, IncludePath(5));

        Assert.Equal(5, document.Included!.Count);
    }

    [Fact]
    public void BuildDocument_throws_when_included_would_exceed_MaxIncludedResources()
    {
        var builder = new ResourceGraphBuilder(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludedResources = 2 });
        var chain = BuildChain(6);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => builder.BuildDocument(chain, IncludePath(5)));
    }

    [Fact]
    public void BuildResource_uses_the_type_override_property_when_set()
    {
        var video = new Media { Id = "1", MediaType = "videos" };

        var resource = _builder.BuildResource(video);

        Assert.Equal("videos", resource.Type);
    }

    [Fact]
    public void BuildResource_falls_back_to_the_declared_resource_type_when_the_override_is_null_or_empty()
    {
        var media = new Media { Id = "1", MediaType = null };

        var resource = _builder.BuildResource(media);

        Assert.Equal("media", resource.Type);
    }

    [Fact]
    public void BuildResource_lets_two_instances_of_the_same_CLR_type_emit_different_type_names()
    {
        var video = new Media { Id = "1", MediaType = "videos" };
        var image = new Media { Id = "2", MediaType = "images" };

        Assert.Equal("videos", _builder.BuildResource(video).Type);
        Assert.Equal("images", _builder.BuildResource(image).Type);
    }

    [Fact]
    public void BuildResource_embeds_the_type_discriminator_for_a_single_valued_polymorphic_attribute()
    {
        var holder = new ShapeHolder { Id = "1", FeaturedShape = new Circle { Radius = 5 } };

        var resource = _builder.BuildResource(holder);

        var shapeNode = Assert.IsAssignableFrom<JsonNode>(resource.Attributes!["featuredShape"]);
        Assert.Equal("circle", shapeNode["$type"]!.GetValue<string>());
        Assert.Equal(5, shapeNode["Radius"]!.GetValue<double>());
    }

    [Fact]
    public void BuildResource_leaves_a_null_polymorphic_attribute_null()
    {
        var holder = new ShapeHolder { Id = "1", FeaturedShape = null };

        var resource = _builder.BuildResource(holder);

        Assert.Null(resource.Attributes!["featuredShape"]);
    }

    [Fact]
    public void Write_serializes_a_collection_valued_polymorphic_attribute_correctly_without_the_fix()
    {
        // Unlike a single-valued polymorphic property, List<Shape> itself carries no
        // [JsonPolymorphic] attribute (only the element type Shape does) — BuildResource leaves it
        // as a raw CLR list, and JsonApiDocumentWriter's `value.GetType()` for the list itself
        // still matches the declared element type per element, so this already worked before the
        // fix. Goes through the full writer (not just BuildResource) since that's where the value
        // actually gets serialized in this case.
        var holder = new ShapeHolder
        {
            Id = "1",
            Shapes = new List<Shape> { new Circle { Radius = 1 }, new Square { Side = 2 } },
        };

        var document = _builder.BuildDocument(holder);
        var json = new JsonApiDocumentWriter().Write(document);

        var array = JsonNode.Parse(json)!["data"]!["attributes"]!["shapes"]!.AsArray();
        Assert.Equal("circle", array[0]!["$type"]!.GetValue<string>());
        Assert.Equal("square", array[1]!["$type"]!.GetValue<string>());
    }

    [Fact]
    public void BuildResource_serializes_heterogeneous_polymorphic_relationship_elements_with_their_own_types()
    {
        var holder = new AttachmentHolder
        {
            Id = "1",
            Attachments = new List<Attachment> { new Video { Id = "1" }, new Image { Id = "2" } },
        };

        var resource = _builder.BuildResource(holder);

        var relationship = resource.Relationships!["attachments"];
        Assert.Equal("videos", relationship.ManyData![0].Type);
        Assert.Equal("images", relationship.ManyData[1].Type);
    }
}
