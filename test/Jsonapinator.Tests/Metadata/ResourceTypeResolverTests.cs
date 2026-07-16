using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Exceptions;
using Jsonapinator.Metadata;

namespace Jsonapinator.Tests.Metadata;

public class ResourceTypeResolverTests
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

        public string NotMapped { get; set; } = "";

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

    private sealed class MissingResourceAttribute
    {
        [JsonApiId]
        public string Id { get; set; } = "";
    }

    [JsonApiResource("no-id")]
    private sealed class MissingIdAttribute
    {
        public string Id { get; set; } = "";
    }

    [JsonApiResource("bad-id")]
    private sealed class UnsupportedIdType
    {
        [JsonApiId]
        public double Id { get; set; }
    }

    [JsonApiResource("guid-ids")]
    private sealed class GuidIdResource
    {
        [JsonApiId]
        public Guid Id { get; set; }
    }

    [Fact]
    public void Resolve_reads_the_resource_type_name()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.Equal("articles", metadata.ResourceType);
    }

    [Fact]
    public void Resolve_finds_the_id_property()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.Id), metadata.IdProperty.Name);
    }

    [Fact]
    public void Resolve_collects_attribute_properties_with_default_camel_case_names()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var title = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.Title));
        Assert.Equal("title", title.JsonName);
    }

    [Fact]
    public void Resolve_respects_JsonPropertyName_override_for_attribute_names()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var wordCount = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.WordCount));
        Assert.Equal("word-count", wordCount.JsonName);
    }

    [Fact]
    public void Resolve_excludes_properties_without_JsonApiAttribute()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.NotMapped));
    }

    [Fact]
    public void Resolve_collects_to_one_relationship_metadata()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var author = Assert.Single(metadata.Relationships, r => r.Name == "author");
        Assert.Equal(RelationshipKind.ToOne, author.Kind);
        Assert.Equal(typeof(Person), author.RelatedClrType);
    }

    [Fact]
    public void Resolve_collects_to_many_relationship_metadata_using_the_element_type()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var comments = Assert.Single(metadata.Relationships, r => r.Name == "comments");
        Assert.Equal(RelationshipKind.ToMany, comments.Kind);
        Assert.Equal(typeof(Comment), comments.RelatedClrType);
    }

    [Fact]
    public void Resolve_caches_metadata_for_the_same_type()
    {
        var resolver = new ResourceTypeResolver();

        var first = resolver.Resolve(typeof(Article));
        var second = resolver.Resolve(typeof(Article));

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_throws_when_JsonApiResource_is_missing()
    {
        var resolver = new ResourceTypeResolver();

        var ex = Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(MissingResourceAttribute)));
        Assert.Contains(nameof(MissingResourceAttribute), ex.Message);
    }

    [Fact]
    public void Resolve_throws_when_JsonApiId_is_missing()
    {
        var resolver = new ResourceTypeResolver();

        var ex = Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(MissingIdAttribute)));
        Assert.Contains(nameof(MissingIdAttribute), ex.Message);
    }

    [Fact]
    public void Resolve_throws_when_id_property_type_is_unsupported()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(UnsupportedIdType)));
    }

    [Theory]
    [InlineData(typeof(Article))]
    [InlineData(typeof(GuidIdResource))]
    public void Resolve_accepts_supported_id_property_types(Type clrType)
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(clrType);

        Assert.NotNull(metadata.IdProperty);
    }
}
