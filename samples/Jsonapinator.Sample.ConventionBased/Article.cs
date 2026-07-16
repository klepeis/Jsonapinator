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
