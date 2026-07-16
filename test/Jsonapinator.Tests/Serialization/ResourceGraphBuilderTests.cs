using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
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
    }

    [JsonApiResource("people")]
    private sealed class Person
    {
        [JsonApiId]
        public string Id { get; set; } = "";
    }

    [JsonApiResource("comments")]
    private sealed class Comment
    {
        [JsonApiId]
        public string Id { get; set; } = "";
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
}
