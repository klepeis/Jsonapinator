using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
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

        [JsonApiMeta]
        public MetaObject ResourceMeta { get; set; } = new();

        [JsonApiLinks]
        public LinksObject ResourceLinks { get; set; } = new();

        [JsonApiRelationshipMeta("comments")]
        public MetaObject CommentsRelationshipMeta { get; set; } = new();

        [JsonApiRelationshipLinks("comments")]
        public LinksObject CommentsRelationshipLinks { get; set; } = new();

        [JsonApiType]
        public string? TypeOverride { get; set; }

        [JsonApiAttribute]
        public Shape? FeaturedShape { get; set; }

        [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
        public List<Attachment> Attachments { get; set; } = new();
    }

    [JsonApiResource("bad-type-type")]
    private sealed class WrongTypeType
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiType]
        public int TypeOverride { get; set; }
    }

    [JsonApiResource("duplicate-type")]
    private sealed class DuplicateType
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiType]
        public string? TypeOne { get; set; }

        [JsonApiType]
        public string? TypeTwo { get; set; }
    }

    [JsonApiResource("bad-meta-type")]
    private sealed class WrongMetaType
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiMeta]
        public string Meta { get; set; } = "";
    }

    [JsonApiResource("bad-links-type")]
    private sealed class WrongLinksType
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiLinks]
        public string Links { get; set; } = "";
    }

    [JsonApiResource("duplicate-meta")]
    private sealed class DuplicateMeta
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiMeta]
        public MetaObject MetaOne { get; set; } = new();

        [JsonApiMeta]
        public MetaObject MetaTwo { get; set; } = new();
    }

    [JsonApiResource("bad-relationship-meta-name")]
    private sealed class BadRelationshipMetaName
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("author", RelationshipKind.ToOne)]
        public Person? Author { get; set; }

        [JsonApiRelationshipMeta("nonexistent")]
        public MetaObject AuthorMeta { get; set; } = new();
    }

    [JsonApiResource("bad-relationship-meta-type")]
    private sealed class BadRelationshipMetaType
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("author", RelationshipKind.ToOne)]
        public Person? Author { get; set; }

        [JsonApiRelationshipMeta("author")]
        public string AuthorMeta { get; set; } = "";
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

    [Fact]
    public void Resolve_finds_the_resource_level_meta_property()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.ResourceMeta), metadata.MetaProperty?.Name);
    }

    [Fact]
    public void Resolve_finds_the_resource_level_links_property()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.ResourceLinks), metadata.LinksProperty?.Name);
    }

    [Fact]
    public void Resolve_leaves_meta_and_links_properties_null_when_not_declared()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Person));

        Assert.Null(metadata.MetaProperty);
        Assert.Null(metadata.LinksProperty);
    }

    [Fact]
    public void Resolve_throws_when_JsonApiMeta_property_type_is_not_MetaObject()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(WrongMetaType)));
    }

    [Fact]
    public void Resolve_throws_when_JsonApiLinks_property_type_is_not_LinksObject()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(WrongLinksType)));
    }

    [Fact]
    public void Resolve_throws_when_multiple_properties_are_decorated_with_JsonApiMeta()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(DuplicateMeta)));
    }

    [Fact]
    public void Resolve_finds_the_relationship_level_meta_and_links_properties()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var comments = Assert.Single(metadata.Relationships, r => r.Name == "comments");
        Assert.Equal(nameof(Article.CommentsRelationshipMeta), comments.MetaProperty?.Name);
        Assert.Equal(nameof(Article.CommentsRelationshipLinks), comments.LinksProperty?.Name);

        var author = Assert.Single(metadata.Relationships, r => r.Name == "author");
        Assert.Null(author.MetaProperty);
        Assert.Null(author.LinksProperty);
    }

    [Fact]
    public void Resolve_throws_when_JsonApiRelationshipMeta_references_an_unknown_relationship()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(BadRelationshipMetaName)));
    }

    [Fact]
    public void Resolve_throws_when_JsonApiRelationshipMeta_property_type_is_not_MetaObject()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(BadRelationshipMetaType)));
    }

    [Fact]
    public void Resolve_finds_the_type_override_property()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.TypeOverride), metadata.TypeProperty?.Name);
    }

    [Fact]
    public void Resolve_leaves_type_override_property_null_when_not_declared()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Person));

        Assert.Null(metadata.TypeProperty);
    }

    [Fact]
    public void Resolve_throws_when_JsonApiType_property_type_is_not_string()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(WrongTypeType)));
    }

    [Fact]
    public void Resolve_throws_when_multiple_properties_are_decorated_with_JsonApiType()
    {
        var resolver = new ResourceTypeResolver();

        Assert.Throws<JsonApiMappingException>(() => resolver.Resolve(typeof(DuplicateType)));
    }

    [Fact]
    public void Resolve_marks_an_attribute_as_polymorphic_when_its_declared_type_is_JsonPolymorphic()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var shape = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.FeaturedShape));
        Assert.True(shape.IsPolymorphic);
    }

    [Fact]
    public void Resolve_does_not_mark_a_plain_attribute_as_polymorphic()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var title = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.Title));
        Assert.False(title.IsPolymorphic);
    }

    [Fact]
    public void Resolve_maps_derived_type_discriminators_for_a_polymorphic_relationship()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var attachments = Assert.Single(metadata.Relationships, r => r.Name == "attachments");
        Assert.NotNull(attachments.PolymorphicDerivedTypes);
        Assert.Equal(typeof(Video), attachments.PolymorphicDerivedTypes!["videos"]);
        Assert.Equal(typeof(Image), attachments.PolymorphicDerivedTypes["images"]);
    }

    [Fact]
    public void Resolve_leaves_PolymorphicDerivedTypes_null_for_a_non_polymorphic_relationship()
    {
        var resolver = new ResourceTypeResolver();

        var metadata = resolver.Resolve(typeof(Article));

        var comments = Assert.Single(metadata.Relationships, r => r.Name == "comments");
        Assert.Null(comments.PolymorphicDerivedTypes);
    }
}
