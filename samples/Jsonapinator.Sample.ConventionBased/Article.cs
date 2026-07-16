using Jsonapinator.Document;

namespace Jsonapinator.Sample.ConventionBased;

// No Jsonapinator.Attributes anywhere on this page — every type here is mapped purely by
// convention. See _docs/convention-based-mapping.md for the classification rule.
public class Article
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    // A plain scalar with no "Id" property -> a flat attribute, not a relationship.
    public DateTime PublishedAtUtc { get; set; }

    // A property whose type has its own "Id" property -> a to-one relationship.
    public Person? Author { get; set; }

    // A collection whose element type has its own "Id" property -> a to-many relationship.
    public List<Comment> Comments { get; set; } = new();

    // A list of plain strings (no "Id" property) -> a flat array attribute, not a relationship.
    public List<string> Tags { get; set; } = new();

    // A nested object with no "Id" property -> a flat nested-object attribute, not a relationship.
    public ArticleSeo Seo { get; set; } = new();

    // A property named exactly "Meta"/"Links" of type MetaObject/LinksObject is recognized by
    // convention as the resource object's own "meta"/"links" (distinct from
    // JsonApiDocumentOptions.Meta/Links, which are document-level). See
    // _docs/convention-based-mapping.md.
    public MetaObject? Meta { get; set; }

    public LinksObject? Links { get; set; }

    // "{RelationshipPropertyName}Meta"/"{RelationshipPropertyName}Links" sibling properties are
    // recognized by convention as that relationship's own "meta"/"links".
    public MetaObject? CommentsMeta { get; set; }

    public LinksObject? CommentsLinks { get; set; }

    public List<Attachment> Attachments { get; set; } = new();
}

// A property named exactly "Type" of type string is recognized by convention as a per-instance
// override of the resource's "type" -- useful for a discriminator-style CLR type shared by
// several JSON:API resource types (here, one Attachment class emits "videos" or "images"
// depending on the instance). Falls back to the usual camelCase-class-name default ("attachment")
// when the property is null/empty. See _docs/convention-based-mapping.md.
public class Attachment
{
    public string Id { get; set; } = "";

    public string Url { get; set; } = "";

    public string? Type { get; set; }
}

public class ArticleSeo
{
    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";
}

public class Person
{
    public string Id { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";
}

public class Comment
{
    public string Id { get; set; } = "";

    public string Body { get; set; } = "";

    public Person? Author { get; set; }
}

// A resource keyed by Guid instead of string, to show convention-based id type coverage
// (string, Guid, int, long are all supported).
public class PrintRun
{
    public Guid Id { get; set; }

    public int CopyCount { get; set; }
}
