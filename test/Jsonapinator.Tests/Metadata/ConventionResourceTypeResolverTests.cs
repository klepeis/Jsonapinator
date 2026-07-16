using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Exceptions;
using Jsonapinator.Metadata;

namespace Jsonapinator.Tests.Metadata;

public class ConventionResourceTypeResolverTests
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

    private sealed class Tag
    {
        // No Id property -> never a relationship target.
        public string Name { get; set; } = "";
    }

    private sealed class Address
    {
        // No Id property -> never a relationship target.
        public string Street { get; set; } = "";
    }

    private sealed class Article
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        [JsonPropertyName("word-count")]
        public int WordCount { get; set; }

        public Person? Author { get; set; }

        public List<Comment> Comments { get; set; } = new();

        public Comment[] PinnedComments { get; set; } = Array.Empty<Comment>();

        public Address? ShippingAddress { get; set; }

        public List<Tag> Tags { get; set; } = new();

        public DateTime PublishedAtUtc { get; set; }

        public string ReadOnlyComputed => Title + "!";

        public string WriteOnly { set { } }

        public string this[int index]
        {
            get => "";
            set { }
        }

        public MetaObject? Meta { get; set; }

        public LinksObject? Links { get; set; }

        public MetaObject? CommentsMeta { get; set; }

        public LinksObject? CommentsLinks { get; set; }

        public string? Type { get; set; }
    }

    private sealed class WrongTypedMetaAndLinks
    {
        public string Id { get; set; } = "";

        // Named "Meta"/"Links" but the wrong type — should fall through to being a flat attribute
        // rather than being recognized as resource-level meta/links.
        public string Meta { get; set; } = "";

        public string Links { get; set; } = "";
    }

    private sealed class WrongTypedType
    {
        public string Id { get; set; } = "";

        // Named "Type" but the wrong type — should fall through to being a flat attribute rather
        // than being recognized as the resource-type override.
        public int Type { get; set; }
    }

    private sealed class OrderLine
    {
        public string Id { get; set; } = "";
    }

    private sealed class NoIdProperty
    {
        public string Name { get; set; } = "";
    }

    private sealed class UnsupportedIdType
    {
        public double Id { get; set; }
    }

    private sealed class GuidDateTimeDecimalThing
    {
        public string Id { get; set; } = "";

        public Guid SomeGuid { get; set; }

        public DateTime SomeDate { get; set; }

        public decimal SomeDecimal { get; set; }
    }

    private readonly ConventionResourceTypeResolver _resolver = new();

    [Fact]
    public void Resolve_derives_the_resource_type_name_from_the_camel_cased_class_name()
    {
        Assert.Equal("article", _resolver.Resolve(typeof(Article)).ResourceType);
        Assert.Equal("orderLine", _resolver.Resolve(typeof(OrderLine)).ResourceType);
    }

    [Fact]
    public void Resolve_finds_the_Id_property_by_convention()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Equal("Id", metadata.IdProperty.Name);
    }

    [Fact]
    public void Resolve_throws_when_no_Id_property_exists()
    {
        Assert.Throws<JsonApiMappingException>(() => _resolver.Resolve(typeof(NoIdProperty)));
    }

    [Fact]
    public void Resolve_throws_when_Id_property_type_is_unsupported()
    {
        Assert.Throws<JsonApiMappingException>(() => _resolver.Resolve(typeof(UnsupportedIdType)));
    }

    [Fact]
    public void Resolve_collects_scalar_properties_as_attributes_with_default_camel_case_names()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var title = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.Title));
        Assert.Equal("title", title.JsonName);
    }

    [Fact]
    public void Resolve_respects_JsonPropertyName_override_for_attribute_names()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var wordCount = Assert.Single(metadata.Attributes, a => a.Property.Name == nameof(Article.WordCount));
        Assert.Equal("word-count", wordCount.JsonName);
    }

    [Fact]
    public void Resolve_excludes_get_only_properties()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.ReadOnlyComputed));
        Assert.DoesNotContain(metadata.Relationships, r => r.Property.Name == nameof(Article.ReadOnlyComputed));
    }

    [Fact]
    public void Resolve_excludes_write_only_properties()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.WriteOnly));
    }

    [Fact]
    public void Resolve_excludes_indexers()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.DoesNotContain(metadata.Attributes, a => a.Property.GetIndexParameters().Length > 0);
        Assert.DoesNotContain(metadata.Relationships, r => r.Property.GetIndexParameters().Length > 0);
    }

    [Fact]
    public void Resolve_detects_a_to_one_relationship_when_the_property_type_has_its_own_Id_property()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var author = Assert.Single(metadata.Relationships, r => r.Property.Name == nameof(Article.Author));
        Assert.Equal("author", author.Name);
        Assert.Equal(RelationshipKind.ToOne, author.Kind);
        Assert.Equal(typeof(Person), author.RelatedClrType);
    }

    [Fact]
    public void Resolve_detects_a_to_many_relationship_for_a_list_of_a_type_with_an_Id_property()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var comments = Assert.Single(metadata.Relationships, r => r.Property.Name == nameof(Article.Comments));
        Assert.Equal("comments", comments.Name);
        Assert.Equal(RelationshipKind.ToMany, comments.Kind);
        Assert.Equal(typeof(Comment), comments.RelatedClrType);
    }

    [Fact]
    public void Resolve_detects_a_to_many_relationship_for_an_array_of_a_type_with_an_Id_property()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var pinned = Assert.Single(metadata.Relationships, r => r.Property.Name == nameof(Article.PinnedComments));
        Assert.Equal(RelationshipKind.ToMany, pinned.Kind);
        Assert.Equal(typeof(Comment), pinned.RelatedClrType);
    }

    [Fact]
    public void Resolve_treats_a_nested_type_without_an_Id_property_as_a_flat_attribute()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Contains(metadata.Attributes, a => a.Property.Name == nameof(Article.ShippingAddress));
        Assert.DoesNotContain(metadata.Relationships, r => r.Property.Name == nameof(Article.ShippingAddress));
    }

    [Fact]
    public void Resolve_treats_a_list_of_a_type_without_an_Id_property_as_a_flat_attribute()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Contains(metadata.Attributes, a => a.Property.Name == nameof(Article.Tags));
        Assert.DoesNotContain(metadata.Relationships, r => r.Property.Name == nameof(Article.Tags));
    }

    [Fact]
    public void Resolve_treats_string_as_an_attribute_not_an_enumerable_relationship()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Contains(metadata.Attributes, a => a.Property.Name == nameof(Article.Title));
    }

    [Fact]
    public void Resolve_treats_common_value_types_as_attributes()
    {
        var metadata = _resolver.Resolve(typeof(GuidDateTimeDecimalThing));

        Assert.Equal(3, metadata.Attributes.Count);
        Assert.Empty(metadata.Relationships);
    }

    [Fact]
    public void Resolve_caches_metadata_for_the_same_type()
    {
        var first = _resolver.Resolve(typeof(Article));
        var second = _resolver.Resolve(typeof(Article));

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_recognizes_a_Meta_property_of_type_MetaObject_as_resource_level_meta()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.Meta), metadata.MetaProperty?.Name);
        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.Meta));
    }

    [Fact]
    public void Resolve_recognizes_a_Links_property_of_type_LinksObject_as_resource_level_links()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.Links), metadata.LinksProperty?.Name);
        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.Links));
    }

    [Fact]
    public void Resolve_falls_through_to_a_flat_attribute_when_Meta_or_Links_named_property_has_the_wrong_type()
    {
        var metadata = _resolver.Resolve(typeof(WrongTypedMetaAndLinks));

        Assert.Null(metadata.MetaProperty);
        Assert.Null(metadata.LinksProperty);
        Assert.Contains(metadata.Attributes, a => a.Property.Name == "Meta");
        Assert.Contains(metadata.Attributes, a => a.Property.Name == "Links");
    }

    [Fact]
    public void Resolve_recognizes_RelName_Meta_and_Links_sibling_properties_for_a_relationship()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        var comments = Assert.Single(metadata.Relationships, r => r.Property.Name == nameof(Article.Comments));
        Assert.Equal(nameof(Article.CommentsMeta), comments.MetaProperty?.Name);
        Assert.Equal(nameof(Article.CommentsLinks), comments.LinksProperty?.Name);

        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.CommentsMeta));
        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.CommentsLinks));

        var author = Assert.Single(metadata.Relationships, r => r.Property.Name == nameof(Article.Author));
        Assert.Null(author.MetaProperty);
        Assert.Null(author.LinksProperty);
    }

    [Fact]
    public void Resolve_recognizes_a_Type_property_of_type_string_as_the_type_override()
    {
        var metadata = _resolver.Resolve(typeof(Article));

        Assert.Equal(nameof(Article.Type), metadata.TypeProperty?.Name);
        Assert.DoesNotContain(metadata.Attributes, a => a.Property.Name == nameof(Article.Type));
    }

    [Fact]
    public void Resolve_falls_through_to_a_flat_attribute_when_Type_named_property_has_the_wrong_type()
    {
        var metadata = _resolver.Resolve(typeof(WrongTypedType));

        Assert.Null(metadata.TypeProperty);
        Assert.Contains(metadata.Attributes, a => a.Property.Name == "Type");
    }

    [Fact]
    public async Task Resolve_is_safe_under_concurrent_first_time_resolution_of_the_same_type()
    {
        var resolver = new ConventionResourceTypeResolver();

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => resolver.Resolve(typeof(Article))))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Same(results[0], r));
    }
}
