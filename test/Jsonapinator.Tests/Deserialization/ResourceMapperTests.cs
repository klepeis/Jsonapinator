using Jsonapinator.Attributes;
using Jsonapinator.Deserialization;
using Jsonapinator.Document;
using Jsonapinator.Metadata;

namespace Jsonapinator.Tests.Deserialization;

public class ResourceMapperTests
{
    [JsonApiResource("articles")]
    private sealed class Article
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Title { get; set; } = "seed-title";

        [JsonApiAttribute]
        public int WordCount { get; set; } = -1;

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

    private readonly ResourceMapper _mapper = new(new ResourceTypeResolver());
    private readonly JsonApiDocumentReader _reader = new();

    private ResourceObject ReadResource(string json) => _reader.Read(json).Data!.Single!;

    [Fact]
    public void Map_sets_the_id_property()
    {
        var resource = ReadResource("""{"data":{"type":"articles","id":"1"}}""");

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("1", article.Id);
    }

    [Fact]
    public void Map_parses_non_string_id_types()
    {
        var resource = ReadResource("""{"data":{"type":"guid-things","id":"11111111-1111-1111-1111-111111111111"}}""");

        var thing = _mapper.Map<GuidThing>(resource);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), thing.Id);
    }

    [Fact]
    public void Map_sets_attributes_present_in_the_json()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","attributes":{"title":"Updated Title","wordCount":99}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("Updated Title", article.Title);
        Assert.Equal(99, article.WordCount);
    }

    [Fact]
    public void Map_leaves_properties_untouched_when_their_key_is_absent_supporting_patch_semantics()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","attributes":{"title":"Only Title Changed"}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("Only Title Changed", article.Title);
        Assert.Equal(-1, article.WordCount);
    }

    [Fact]
    public void Map_builds_a_to_one_relationship_stub_with_only_the_id_set()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("9", article.Author!.Id);
    }

    [Fact]
    public void Map_sets_a_to_one_relationship_to_null_when_data_is_null()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":null}}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Null(article.Author);
    }

    [Fact]
    public void Map_builds_to_many_relationship_stubs()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{"comments":{"data":[{"type":"comments","id":"5"},{"type":"comments","id":"12"}]}}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal(2, article.Comments.Count);
        Assert.Equal("5", article.Comments[0].Id);
        Assert.Equal("12", article.Comments[1].Id);
    }
}
