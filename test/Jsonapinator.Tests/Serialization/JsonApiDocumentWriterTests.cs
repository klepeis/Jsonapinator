using System.Text.Json.Nodes;
using Jsonapinator.Document;
using Jsonapinator.Serialization;

namespace Jsonapinator.Tests.Serialization;

public class JsonApiDocumentWriterTests
{
    private readonly JsonApiDocumentWriter _writer = new();

    private JsonNode Write(JsonApiDocument document) => JsonNode.Parse(_writer.Write(document))!;

    [Fact]
    public void Writes_a_single_resource_with_only_type_and_id()
    {
        var document = JsonApiDocument.ForSingleResource(new ResourceObject { Type = "articles", Id = "1" });

        var json = Write(document);

        Assert.Equal("articles", json["data"]!["type"]!.GetValue<string>());
        Assert.Equal("1", json["data"]!["id"]!.GetValue<string>());
        Assert.Null(json["data"]!["attributes"]);
    }

    [Fact]
    public void Writes_null_data_for_a_null_single_resource()
    {
        var document = JsonApiDocument.ForSingleResource(null);

        var json = Write(document);

        Assert.True(json.AsObject().ContainsKey("data"));
        Assert.Null(json["data"]);
    }

    [Fact]
    public void Writes_attributes_object_when_present()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Attributes = new Dictionary<string, object?> { ["title"] = "JSON:API paints my bikeshed!" },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        Assert.Equal("JSON:API paints my bikeshed!", json["data"]!["attributes"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_a_non_null_to_one_relationship_as_a_resource_identifier()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Relationships = new Dictionary<string, RelationshipObject>
            {
                ["author"] = new()
                {
                    IsToMany = false,
                    SingleData = new ResourceIdentifierObject { Type = "people", Id = "9" },
                },
            },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        var authorData = json["data"]!["relationships"]!["author"]!["data"]!;
        Assert.Equal("people", authorData["type"]!.GetValue<string>());
        Assert.Equal("9", authorData["id"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_a_null_to_one_relationship_as_null_data()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Relationships = new Dictionary<string, RelationshipObject>
            {
                ["author"] = new() { IsToMany = false, SingleData = null },
            },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        var relationship = json["data"]!["relationships"]!["author"]!.AsObject();
        Assert.True(relationship.ContainsKey("data"));
        Assert.Null(relationship["data"]);
    }

    [Fact]
    public void Writes_an_empty_to_many_relationship_as_an_empty_array()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Relationships = new Dictionary<string, RelationshipObject>
            {
                ["comments"] = new() { IsToMany = true, ManyData = new List<ResourceIdentifierObject>() },
            },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        var data = json["data"]!["relationships"]!["comments"]!["data"]!.AsArray();
        Assert.Empty(data);
    }

    [Fact]
    public void Writes_a_populated_to_many_relationship_as_an_array_of_identifiers()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Relationships = new Dictionary<string, RelationshipObject>
            {
                ["comments"] = new()
                {
                    IsToMany = true,
                    ManyData = new List<ResourceIdentifierObject>
                    {
                        new() { Type = "comments", Id = "5" },
                        new() { Type = "comments", Id = "12" },
                    },
                },
            },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        var data = json["data"]!["relationships"]!["comments"]!["data"]!.AsArray();
        Assert.Equal(2, data.Count);
        Assert.Equal("5", data[0]!["id"]!.GetValue<string>());
        Assert.Equal("12", data[1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_a_resource_collection_as_a_json_array()
    {
        var resources = new List<ResourceObject>
        {
            new() { Type = "articles", Id = "1" },
            new() { Type = "articles", Id = "2" },
        };

        var json = Write(JsonApiDocument.ForCollection(resources));

        var data = json["data"]!.AsArray();
        Assert.Equal(2, data.Count);
        Assert.Equal("1", data[0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_an_empty_collection_as_an_empty_array()
    {
        var json = Write(JsonApiDocument.ForCollection(Enumerable.Empty<ResourceObject>()));

        Assert.Empty(json["data"]!.AsArray());
    }

    [Fact]
    public void Writes_document_level_links_and_meta()
    {
        var document = JsonApiDocument.ForSingleResource(new ResourceObject { Type = "articles", Id = "1" });
        document.Links = new LinksObject { ["self"] = "http://example.com/articles/1" };
        document.Meta = new MetaObject { ["copyright"] = "Copyright 2026" };

        var json = Write(document);

        Assert.Equal("http://example.com/articles/1", json["links"]!["self"]!.GetValue<string>());
        Assert.Equal("Copyright 2026", json["meta"]!["copyright"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_resource_level_links_and_meta()
    {
        var resource = new ResourceObject
        {
            Type = "articles",
            Id = "1",
            Links = new LinksObject { ["self"] = "http://example.com/articles/1" },
            Meta = new MetaObject { ["views"] = 42 },
        };

        var json = Write(JsonApiDocument.ForSingleResource(resource));

        Assert.Equal("http://example.com/articles/1", json["data"]!["links"]!["self"]!.GetValue<string>());
        Assert.Equal(42, json["data"]!["meta"]!["views"]!.GetValue<int>());
    }

    [Fact]
    public void Writes_included_array_when_present()
    {
        var document = JsonApiDocument.ForSingleResource(new ResourceObject { Type = "articles", Id = "1" });
        document.Included = new List<ResourceObject>
        {
            new()
            {
                Type = "people",
                Id = "9",
                Attributes = new Dictionary<string, object?> { ["firstName"] = "Dan" },
            },
        };

        var json = Write(document);

        var included = json["included"]!.AsArray();
        Assert.Single(included);
        Assert.Equal("people", included[0]!["type"]!.GetValue<string>());
        Assert.Equal("Dan", included[0]!["attributes"]!["firstName"]!.GetValue<string>());
    }

    [Fact]
    public void Omits_included_member_when_null()
    {
        var document = JsonApiDocument.ForSingleResource(new ResourceObject { Type = "articles", Id = "1" });

        var json = Write(document);

        Assert.False(json.AsObject().ContainsKey("included"));
    }

    [Fact]
    public void Writes_errors_without_a_data_member()
    {
        var document = JsonApiDocument.ForErrors(new List<ErrorObject>
        {
            new() { Status = "422", Title = "Invalid Attribute", Detail = "Title must not be blank." },
        });

        var json = Write(document);

        Assert.False(json.AsObject().ContainsKey("data"));
        var error = json["errors"]!.AsArray()[0]!;
        Assert.Equal("422", error["status"]!.GetValue<string>());
        Assert.Equal("Invalid Attribute", error["title"]!.GetValue<string>());
        Assert.Equal("Title must not be blank.", error["detail"]!.GetValue<string>());
    }

    [Fact]
    public void Writes_error_source_pointer()
    {
        var document = JsonApiDocument.ForErrors(new List<ErrorObject>
        {
            new() { Status = "400", Source = new ErrorSourceObject { Pointer = "/data/attributes/title" } },
        });

        var json = Write(document);

        Assert.Equal("/data/attributes/title", json["errors"]![0]!["source"]!["pointer"]!.GetValue<string>());
    }
}
