using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;

namespace Jsonapinator.Sample.AttributeBased;

// The same conceptual resource graph as Jsonapinator.Sample.ConventionBased, mapped explicitly
// instead. See _docs/attribute-based-mapping.md for the full attribute reference.
[JsonApiResource("articles")]
public class Article
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Title { get; set; } = "";

    // [JsonPropertyName] overrides the default camelCase JSON attribute name.
    [JsonApiAttribute]
    [JsonPropertyName("published-at")]
    public DateTime PublishedAtUtc { get; set; }

    [JsonApiRelationship("author", RelationshipKind.ToOne)]
    public Person? Author { get; set; }

    [JsonApiRelationship("comments", RelationshipKind.ToMany)]
    public List<Comment> Comments { get; set; } = new();

    // A list of plain strings and a nested object -- serialized as-is via System.Text.Json,
    // same as convention mode, once explicitly marked as attributes.
    [JsonApiAttribute]
    public List<string> Tags { get; set; } = new();

    [JsonApiAttribute]
    public ArticleSeo Seo { get; set; } = new();

    // Not marked with [JsonApiAttribute]/[JsonApiRelationship] -> never appears in the JSON:API
    // document at all (unlike convention mode, attribute mode requires an explicit attribute on
    // every property that should be exposed).
    public string InternalNotes { get; set; } = "";

    // [JsonApiMeta]/[JsonApiLinks] lift a property straight onto the resource object's own
    // "meta"/"links" (as opposed to JsonApiDocumentOptions.Meta/Links, which are document-level).
    // See _docs/attribute-based-mapping.md.
    [JsonApiMeta]
    public MetaObject? ArticleMeta { get; set; }

    [JsonApiLinks]
    public LinksObject? ArticleLinks { get; set; }

    // [JsonApiRelationshipMeta]/[JsonApiRelationshipLinks] attach to the "comments" relationship
    // object itself (its own "meta"/"links", not the related Comment resources' own meta/links).
    [JsonApiRelationshipMeta("comments")]
    public MetaObject? CommentsMeta { get; set; }

    [JsonApiRelationshipLinks("comments")]
    public LinksObject? CommentsLinks { get; set; }

    [JsonApiRelationship("attachments", RelationshipKind.ToMany)]
    public List<Attachment> Attachments { get; set; } = new();
}

// [JsonApiType] overrides the resource's "type" per instance instead of using a fixed
// [JsonApiResource] name -- useful for a discriminator-style CLR type shared by several JSON:API
// resource types (here, one Attachment class emits "videos" or "images" depending on the
// instance). Falls back to the [JsonApiResource] name below when the property is null/empty.
// See _docs/attribute-based-mapping.md.
[JsonApiResource("attachments")]
public class Attachment
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Url { get; set; } = "";

    [JsonApiType]
    public string? AttachmentType { get; set; }
}

public class ArticleSeo
{
    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";
}

[JsonApiResource("people")]
public class Person
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string FirstName { get; set; } = "";

    [JsonApiAttribute]
    public string LastName { get; set; } = "";
}

[JsonApiResource("comments")]
public class Comment
{
    [JsonApiId]
    public string Id { get; set; } = "";

    [JsonApiAttribute]
    public string Body { get; set; } = "";

    [JsonApiRelationship("author", RelationshipKind.ToOne)]
    public Person? Author { get; set; }
}

// A Guid-keyed resource, mirroring Jsonapinator.Sample.ConventionBased's PrintRun.
[JsonApiResource("print-runs")]
public class PrintRun
{
    [JsonApiId]
    public Guid Id { get; set; }

    [JsonApiAttribute]
    public int CopyCount { get; set; }
}
