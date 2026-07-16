namespace Jsonapinator.Document;

/// <summary>
/// A JSON:API "error object" (https://jsonapi.org/format/#errors).
/// </summary>
public sealed class ErrorObject
{
    public string? Id { get; set; }

    public string? Status { get; set; }

    public string? Code { get; set; }

    public string? Title { get; set; }

    public string? Detail { get; set; }

    public ErrorSourceObject? Source { get; set; }

    public MetaObject? Meta { get; set; }
}
