using Jsonapinator.Deserialization;
using Jsonapinator.Exceptions;

namespace Jsonapinator.Tests.Deserialization;

public class JsonApiDocumentReaderTests
{
    private readonly JsonApiDocumentReader _reader = new();

    [Fact]
    public void Parses_a_minimal_single_resource_document()
    {
        var document = _reader.Read("""{"data":{"type":"articles","id":"1"}}""");

        Assert.NotNull(document.Data);
        Assert.False(document.Data!.IsCollection);
        Assert.Equal("articles", document.Data.Single!.Type);
        Assert.Equal("1", document.Data.Single.Id);
    }

    [Fact]
    public void Parses_attributes_on_a_resource()
    {
        var document = _reader.Read("""
            {"data":{"type":"articles","id":"1","attributes":{"title":"Bikeshedding","word-count":42}}}
            """);

        Assert.True(document.Data!.Single!.Attributes!.ContainsKey("title"));
        Assert.True(document.Data.Single.Attributes.ContainsKey("word-count"));
    }

    [Fact]
    public void Parses_a_null_single_resource()
    {
        var document = _reader.Read("""{"data":null}""");

        Assert.NotNull(document.Data);
        Assert.False(document.Data!.IsCollection);
        Assert.Null(document.Data.Single);
    }

    [Fact]
    public void Parses_a_resource_collection()
    {
        var document = _reader.Read("""
            {"data":[{"type":"articles","id":"1"},{"type":"articles","id":"2"}]}
            """);

        Assert.True(document.Data!.IsCollection);
        Assert.Equal(2, document.Data.Collection!.Count);
    }

    [Fact]
    public void Parses_an_empty_resource_collection()
    {
        var document = _reader.Read("""{"data":[]}""");

        Assert.True(document.Data!.IsCollection);
        Assert.Empty(document.Data.Collection!);
    }

    [Fact]
    public void Parses_a_to_one_relationship()
    {
        var document = _reader.Read("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}}}
            """);

        var relationship = document.Data!.Single!.Relationships!["author"];
        Assert.False(relationship.IsToMany);
        Assert.Equal("9", relationship.SingleData!.Id);
    }

    [Fact]
    public void Parses_a_to_many_relationship()
    {
        var document = _reader.Read("""
            {"data":{"type":"articles","id":"1","relationships":{"comments":{"data":[{"type":"comments","id":"5"},{"type":"comments","id":"12"}]}}}}
            """);

        var relationship = document.Data!.Single!.Relationships!["comments"];
        Assert.True(relationship.IsToMany);
        Assert.Equal(2, relationship.ManyData!.Count);
    }

    [Fact]
    public void Parses_the_included_array()
    {
        var document = _reader.Read("""
            {"data":{"type":"articles","id":"1"},"included":[{"type":"people","id":"9","attributes":{"firstName":"Dan"}}]}
            """);

        var included = Assert.Single(document.Included!);
        Assert.Equal("people", included.Type);
        Assert.Equal("9", included.Id);
    }

    [Fact]
    public void Leaves_included_null_when_absent()
    {
        var document = _reader.Read("""{"data":{"type":"articles","id":"1"}}""");

        Assert.Null(document.Included);
    }

    [Fact]
    public void Parses_errors()
    {
        var document = _reader.Read("""
            {"errors":[{"status":"422","title":"Invalid Attribute","source":{"pointer":"/data/attributes/title"}}]}
            """);

        Assert.Null(document.Data);
        var error = Assert.Single(document.Errors!);
        Assert.Equal("422", error.Status);
        Assert.Equal("/data/attributes/title", error.Source!.Pointer);
    }

    [Fact]
    public void Parses_document_level_meta_and_links()
    {
        var document = _reader.Read("""
            {"data":{"type":"articles","id":"1"},"meta":{"copyright":"Copyright 2026"},"links":{"self":"http://example.com/articles/1"}}
            """);

        Assert.Equal("Copyright 2026", document.Meta!["copyright"]!.ToString());
        Assert.Equal("http://example.com/articles/1", document.Links!["self"]);
    }

    [Fact]
    public void Throws_a_mapping_exception_for_malformed_json()
    {
        Assert.Throws<JsonApiMappingException>(() => _reader.Read("not json"));
    }
}
