using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

        [JsonApiMeta]
        public MetaObject? ResourceMeta { get; set; }

        [JsonApiLinks]
        public LinksObject? ResourceLinks { get; set; }

        [JsonApiRelationshipMeta("comments")]
        public MetaObject? CommentsMeta { get; set; }

        [JsonApiRelationshipLinks("comments")]
        public LinksObject? CommentsLinks { get; set; }

        [JsonApiAttribute]
        public ArticleSeo? Seo { get; set; }
    }

    private sealed class ArticleSeo
    {
        public string MetaTitle { get; set; } = "";

        public string MetaDescription { get; set; } = "";
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

        [JsonApiRelationship("article", RelationshipKind.ToOne)]
        public Article? Article { get; set; }
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

    [JsonApiResource("attachment-holders")]
    private sealed class AttachmentHolder
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("attachment", RelationshipKind.ToOne)]
        public Media? Attachment { get; set; }
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
        [JsonApiAttribute]
        public string Resolution { get; set; } = "";
    }

    [JsonApiResource("gallery-articles")]
    private sealed class GalleryArticle
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
        public List<Attachment> Attachments { get; set; } = new();

        [JsonApiRelationship("primaryAttachment", RelationshipKind.ToOne)]
        public Attachment? PrimaryAttachment { get; set; }
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

    private JsonApiDocument ReadDocument(string json) => _reader.Read(json);

    [Fact]
    public void Map_with_runtime_Type_sets_the_id_property()
    {
        var resource = ReadResource("""{"data":{"type":"articles","id":"1"}}""");

        var article = (Article)_mapper.Map(typeof(Article), resource);

        Assert.Equal("1", article.Id);
    }

    [Fact]
    public void Map_with_runtime_Type_hydrates_relationships_from_included()
    {
        var document = ReadDocument("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}},
             "included":[{"type":"people","id":"9","attributes":{"firstName":"Dan"}}]}
            """);

        var article = (Article)_mapper.Map(typeof(Article), document.Data!.Single!, document.Included);

        Assert.Equal("Dan", article.Author!.FirstName);
    }

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
    public void Map_throws_JsonApiMappingException_not_a_raw_JsonException_for_a_wrong_typed_attribute()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","attributes":{"wordCount":"not-a-number"}}}
            """);

        var ex = Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(() => _mapper.Map<Article>(resource));
        Assert.Contains("wordCount", ex.Message);
    }

    [Fact]
    public void Map_reads_a_camelCased_nested_attribute_value()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","attributes":{
                "seo":{"metaTitle":"Title","metaDescription":"Description"}
            }}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("Title", article.Seo!.MetaTitle);
        Assert.Equal("Description", article.Seo.MetaDescription);
    }

    [Fact]
    public void Map_reads_a_pre_existing_PascalCased_nested_attribute_value_for_backward_compatibility()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","attributes":{
                "seo":{"MetaTitle":"Title","MetaDescription":"Description"}
            }}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal("Title", article.Seo!.MetaTitle);
        Assert.Equal("Description", article.Seo.MetaDescription);
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

    [Fact]
    public void Map_hydrates_a_relationship_from_the_included_array()
    {
        var document = ReadDocument("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}},
             "included":[{"type":"people","id":"9","attributes":{"firstName":"Dan"}}]}
            """);

        var article = _mapper.Map<Article>(document.Data!.Single!, document.Included);

        Assert.Equal("9", article.Author!.Id);
        Assert.Equal("Dan", article.Author.FirstName);
    }

    [Fact]
    public void Map_falls_back_to_a_stub_when_included_has_no_matching_entry()
    {
        var document = ReadDocument("""
            {"data":{"type":"articles","id":"1","relationships":{"author":{"data":{"type":"people","id":"9"}}}},
             "included":[{"type":"people","id":"999","attributes":{"firstName":"Someone Else"}}]}
            """);

        var article = _mapper.Map<Article>(document.Data!.Single!, document.Included);

        Assert.Equal("9", article.Author!.Id);
        Assert.Equal("", article.Author.FirstName);
    }

    [Fact]
    public void Map_hydrates_nested_relationships_from_included()
    {
        var document = ReadDocument("""
            {"data":{"type":"articles","id":"1","relationships":{"comments":{"data":[{"type":"comments","id":"5"}]}}},
             "included":[
                {"type":"comments","id":"5","attributes":{"body":"First!"},"relationships":{"author":{"data":{"type":"people","id":"9"}}}},
                {"type":"people","id":"9","attributes":{"firstName":"Dan"}}
             ]}
            """);

        var article = _mapper.Map<Article>(document.Data!.Single!, document.Included);

        Assert.Equal("First!", article.Comments[0].Body);
        Assert.Equal("Dan", article.Comments[0].Author!.FirstName);
    }

    [Fact]
    public void Map_does_not_infinitely_recurse_on_a_circular_reference()
    {
        var document = ReadDocument("""
            {"data":{"type":"articles","id":"1","attributes":{"title":"Root"},"relationships":{"comments":{"data":[{"type":"comments","id":"5"}]}}},
             "included":[
                {"type":"comments","id":"5","attributes":{"body":"First!"},"relationships":{"article":{"data":{"type":"articles","id":"1"}}}}
             ]}
            """);

        var article = _mapper.Map<Article>(document.Data!.Single!, document.Included);

        // The cyclic back-reference (comment -> article -> comment -> ...) is hydrated one
        // level deep (a "gray node" re-entrancy guard, not a permanent "seen" set — see
        // ResourceMapper), then the repeat encounter is left as an id-only stub so recursion
        // terminates instead of overflowing the stack.
        Assert.Equal("Root", article.Title);
        Assert.Equal("First!", article.Comments[0].Body);
        Assert.Equal("1", article.Comments[0].Article!.Id);
        Assert.Equal("Root", article.Comments[0].Article!.Title);
        Assert.Equal("5", article.Comments[0].Article!.Comments[0].Id);
        Assert.Equal("", article.Comments[0].Article!.Comments[0].Body);
    }

    [Fact]
    public void Map_sets_resource_level_meta_and_links_when_present()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","meta":{"views":42},"links":{"self":"/articles/1"}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal(42, ((JsonNode)article.ResourceMeta!["views"]!).GetValue<int>());
        Assert.Equal("/articles/1", article.ResourceLinks!["self"]);
    }

    [Fact]
    public void Map_leaves_resource_level_meta_and_links_untouched_when_absent()
    {
        var resource = ReadResource("""{"data":{"type":"articles","id":"1"}}""");

        var article = _mapper.Map<Article>(resource);

        Assert.Null(article.ResourceMeta);
        Assert.Null(article.ResourceLinks);
    }

    [Fact]
    public void Map_sets_relationship_level_meta_and_links_when_present()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{
                "comments":{"data":[],"meta":{"count":0},"links":{"self":"/articles/1/relationships/comments"}}
            }}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Equal(0, ((JsonNode)article.CommentsMeta!["count"]!).GetValue<int>());
        Assert.Equal("/articles/1/relationships/comments", article.CommentsLinks!["self"]);
    }

    [Fact]
    public void Map_leaves_relationship_level_meta_and_links_untouched_when_absent()
    {
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{"comments":{"data":[]}}}}
            """);

        var article = _mapper.Map<Article>(resource);

        Assert.Null(article.CommentsMeta);
        Assert.Null(article.CommentsLinks);
    }

    // Builds a linear chain of `count` distinct chain-node resources (1 -> 2 -> 3 -> ... -> count),
    // each referencing the next via a to-one "next" relationship, with the root as primary data
    // and the rest in "included". Distinct ids mean this is not a cycle (the existing `visiting`
    // guard never fires) -- it's the "long linear chain" shape the depth limit exists for.
    private static JsonApiDocument BuildChain(int count)
    {
        var nodes = new JsonArray();
        for (var i = 1; i <= count; i++)
        {
            var next = i < count
                ? new JsonObject { ["data"] = new JsonObject { ["type"] = "chain-nodes", ["id"] = (i + 1).ToString() } }
                : new JsonObject { ["data"] = null };

            nodes.Add(new JsonObject
            {
                ["type"] = "chain-nodes",
                ["id"] = i.ToString(),
                ["relationships"] = new JsonObject { ["next"] = next },
            });
        }

        var root = (JsonObject)nodes[0]!.DeepClone();
        var included = new JsonArray();
        for (var i = 1; i < nodes.Count; i++)
        {
            included.Add(nodes[i]!.DeepClone());
        }

        var document = new JsonObject { ["data"] = root, ["included"] = included };
        return new JsonApiDocumentReader().Read(document.ToJsonString());
    }

    [Fact]
    public void Map_throws_when_a_linear_included_chain_exceeds_MaxIncludeDepth()
    {
        var mapper = new ResourceMapper(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludeDepth = 3 });
        var document = BuildChain(6);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => mapper.Map<ChainNode>(document.Data!.Single!, document.Included));
    }

    [Fact]
    public void Map_succeeds_when_a_linear_included_chain_is_within_MaxIncludeDepth()
    {
        var mapper = new ResourceMapper(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludeDepth = 10 });
        var document = BuildChain(6);

        var chain = mapper.Map<ChainNode>(document.Data!.Single!, document.Included);

        Assert.Equal("1", chain.Id);
        Assert.Equal("6", chain.Next!.Next!.Next!.Next!.Next!.Id);
    }

    [Fact]
    public void Map_throws_when_the_included_array_exceeds_MaxIncludedResources()
    {
        var mapper = new ResourceMapper(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxIncludedResources = 2 });
        var document = new JsonApiDocumentReader().Read("""
            {"data":{"type":"articles","id":"1"},
             "included":[
                {"type":"people","id":"1"},
                {"type":"people","id":"2"},
                {"type":"people","id":"3"}
             ]}
            """);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(
            () => mapper.Map<Article>(document.Data!.Single!, document.Included));
    }

    [Fact]
    public void Map_throws_when_a_to_many_relationship_array_exceeds_MaxToManyRelationshipSize()
    {
        var mapper = new ResourceMapper(new ResourceTypeResolver(), new JsonApiSerializerOptions { MaxToManyRelationshipSize = 2 });
        var resource = ReadResource("""
            {"data":{"type":"articles","id":"1","relationships":{"comments":{"data":[
                {"type":"comments","id":"1"},{"type":"comments","id":"2"},{"type":"comments","id":"3"}
            ]}}}}
            """);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(() => mapper.Map<Article>(resource));
    }

    [Fact]
    public void Map_sets_the_type_override_property_from_the_incoming_type()
    {
        var resource = ReadResource("""{"data":{"type":"videos","id":"1"}}""");

        var media = _mapper.Map<Media>(resource);

        Assert.Equal("videos", media.MediaType);
    }

    [Fact]
    public void Map_sets_the_type_override_property_on_an_identifier_only_stub()
    {
        var resource = ReadResource("""
            {"data":{"type":"attachment-holders","id":"1","relationships":{
                "attachment":{"data":{"type":"images","id":"9"}}
            }}}
            """);

        var holder = _mapper.Map<AttachmentHolder>(resource);

        Assert.Equal("9", holder.Attachment!.Id);
        Assert.Equal("images", holder.Attachment.MediaType);
    }

    [Fact]
    public void Map_sets_the_type_override_property_when_hydrated_from_included()
    {
        var root = new JsonObject
        {
            ["type"] = "attachment-holders",
            ["id"] = "1",
            ["relationships"] = new JsonObject
            {
                ["attachment"] = new JsonObject
                {
                    ["data"] = new JsonObject { ["type"] = "images", ["id"] = "9" },
                },
            },
        };
        var included = new JsonArray { new JsonObject { ["type"] = "images", ["id"] = "9" } };
        var documentJson = new JsonObject { ["data"] = root, ["included"] = included }.ToJsonString();

        var document = new JsonApiDocumentReader().Read(documentJson);
        var holder = _mapper.Map<AttachmentHolder>(document.Data!.Single!, document.Included);

        Assert.Equal("images", holder.Attachment!.MediaType);
    }

    [Fact]
    public void Map_resolves_a_to_many_polymorphic_relationship_to_the_correct_concrete_subtypes()
    {
        var resource = ReadResource("""
            {"data":{"type":"gallery-articles","id":"1","relationships":{"attachments":{"data":[
                {"type":"videos","id":"1"},{"type":"images","id":"2"}
            ]}}}}
            """);

        var article = _mapper.Map<GalleryArticle>(resource);

        Assert.IsType<Video>(article.Attachments[0]);
        Assert.IsType<Image>(article.Attachments[1]);
        Assert.Equal("1", article.Attachments[0].Id);
        Assert.Equal("2", article.Attachments[1].Id);
    }

    [Fact]
    public void Map_resolves_a_to_one_polymorphic_relationship_to_the_correct_concrete_subtype()
    {
        var resource = ReadResource("""
            {"data":{"type":"gallery-articles","id":"1","relationships":{
                "primaryAttachment":{"data":{"type":"videos","id":"1"}}
            }}}
            """);

        var article = _mapper.Map<GalleryArticle>(resource);

        Assert.IsType<Video>(article.PrimaryAttachment);
    }

    [Fact]
    public void Map_resolves_the_correct_polymorphic_subtype_for_an_identifier_only_stub()
    {
        // No "included" array at all -- proves the discriminator alone (no full attributes) is
        // enough to pick the right concrete type.
        var resource = ReadResource("""
            {"data":{"type":"gallery-articles","id":"1","relationships":{
                "primaryAttachment":{"data":{"type":"images","id":"9"}}
            }}}
            """);

        var article = _mapper.Map<GalleryArticle>(resource);

        Assert.IsType<Image>(article.PrimaryAttachment);
        Assert.Equal("9", article.PrimaryAttachment!.Id);
    }

    [Fact]
    public void Map_hydrates_a_polymorphic_relationship_subtypes_own_attributes_from_included()
    {
        var root = new JsonObject
        {
            ["type"] = "gallery-articles",
            ["id"] = "1",
            ["relationships"] = new JsonObject
            {
                ["primaryAttachment"] = new JsonObject
                {
                    ["data"] = new JsonObject { ["type"] = "videos", ["id"] = "1" },
                },
            },
        };
        var included = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "videos",
                ["id"] = "1",
                ["attributes"] = new JsonObject { ["durationSeconds"] = 42 },
            },
        };
        var documentJson = new JsonObject { ["data"] = root, ["included"] = included }.ToJsonString();

        var document = new JsonApiDocumentReader().Read(documentJson);
        var article = _mapper.Map<GalleryArticle>(document.Data!.Single!, document.Included);

        Assert.Equal(42, ((Video)article.PrimaryAttachment!).DurationSeconds);
    }

    [Fact]
    public void Map_throws_when_a_polymorphic_relationships_discriminator_has_no_matching_JsonDerivedType()
    {
        var resource = ReadResource("""
            {"data":{"type":"gallery-articles","id":"1","relationships":{
                "primaryAttachment":{"data":{"type":"audio-clips","id":"1"}}
            }}}
            """);

        Assert.Throws<Jsonapinator.Exceptions.JsonApiMappingException>(() => _mapper.Map<GalleryArticle>(resource));
    }
}
