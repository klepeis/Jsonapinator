using System.Text.Json.Serialization;
using Jsonapinator.Attributes;

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
