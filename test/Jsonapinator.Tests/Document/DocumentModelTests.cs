using Jsonapinator.Document;

namespace Jsonapinator.Tests.Document;

public class DocumentModelTests
{
    [Fact]
    public void ResourceIdentifierObject_supports_object_initializer_construction()
    {
        var identifier = new ResourceIdentifierObject
        {
            Type = "articles",
            Id = "1",
        };

        Assert.Equal("articles", identifier.Type);
        Assert.Equal("1", identifier.Id);
        Assert.Null(identifier.Meta);
    }

    [Fact]
    public void ResourceObject_supports_attributes_relationships_links_and_meta()
    {
        var relationship = new RelationshipObject
        {
            IsToMany = false,
            SingleData = new ResourceIdentifierObject { Type = "people", Id = "9" },
        };

        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Attributes = new Dictionary<string, object?> { ["title"] = "JSON:API paints my bikeshed!" },
            Relationships = new Dictionary<string, RelationshipObject> { ["author"] = relationship },
            Links = new LinksObject { ["self"] = "http://example.com/articles/1" },
            Meta = new MetaObject { ["copyright"] = "Copyright 2026" },
        };

        Assert.Equal("articles", resource.Type);
        Assert.Equal("1", resource.Id);
        Assert.Equal("JSON:API paints my bikeshed!", resource.Attributes!["title"]);
        Assert.False(resource.Relationships!["author"].IsToMany);
        Assert.Equal("9", resource.Relationships["author"].SingleData!.Id);
        Assert.Equal("http://example.com/articles/1", resource.Links!["self"]);
        Assert.Equal("Copyright 2026", resource.Meta!["copyright"]);
    }

    [Fact]
    public void RelationshipObject_represents_to_many_data_as_a_collection()
    {
        var relationship = new RelationshipObject
        {
            IsToMany = true,
            ManyData = new List<ResourceIdentifierObject>
            {
                new() { Type = "comments", Id = "5" },
                new() { Type = "comments", Id = "12" },
            },
        };

        Assert.True(relationship.IsToMany);
        Assert.Equal(2, relationship.ManyData!.Count);
        Assert.Null(relationship.SingleData);
    }

    [Fact]
    public void JsonApiDocument_ForSingleResource_wraps_a_single_resource()
    {
        var resource = new ResourceObject { Type = "articles", Id = "1" };

        var document = JsonApiDocument.ForSingleResource(resource);

        Assert.NotNull(document.Data);
        Assert.False(document.Data!.IsCollection);
        Assert.Same(resource, document.Data.Single);
        Assert.Null(document.Data.Collection);
        Assert.Null(document.Errors);
    }

    [Fact]
    public void JsonApiDocument_ForSingleResource_allows_null_data()
    {
        var document = JsonApiDocument.ForSingleResource(null);

        Assert.NotNull(document.Data);
        Assert.False(document.Data!.IsCollection);
        Assert.Null(document.Data.Single);
    }

    [Fact]
    public void JsonApiDocument_ForCollection_wraps_a_resource_collection()
    {
        var resources = new List<ResourceObject>
        {
            new() { Type = "articles", Id = "1" },
            new() { Type = "articles", Id = "2" },
        };

        var document = JsonApiDocument.ForCollection(resources);

        Assert.NotNull(document.Data);
        Assert.True(document.Data!.IsCollection);
        Assert.Equal(2, document.Data.Collection!.Count);
        Assert.Null(document.Data.Single);
    }

    [Fact]
    public void JsonApiDocument_ForErrors_wraps_errors_and_leaves_data_null()
    {
        var errors = new List<ErrorObject>
        {
            new() { Status = "422", Title = "Invalid Attribute" },
        };

        var document = JsonApiDocument.ForErrors(errors);

        Assert.Null(document.Data);
        Assert.Single(document.Errors!);
        Assert.Equal("422", document.Errors![0].Status);
    }

    [Fact]
    public void JsonApiDocument_Included_defaults_to_null_and_is_settable()
    {
        var document = JsonApiDocument.ForSingleResource(new ResourceObject { Type = "articles", Id = "1" });

        Assert.Null(document.Included);

        var included = new List<ResourceObject> { new() { Type = "people", Id = "9" } };
        document.Included = included;

        Assert.Same(included, document.Included);
    }

    [Fact]
    public void ErrorObject_supports_source_pointer_and_parameter()
    {
        var error = new ErrorObject
        {
            Status = "400",
            Source = new ErrorSourceObject { Pointer = "/data/attributes/title" },
        };

        Assert.Equal("/data/attributes/title", error.Source!.Pointer);
        Assert.Null(error.Source.Parameter);
    }
}
