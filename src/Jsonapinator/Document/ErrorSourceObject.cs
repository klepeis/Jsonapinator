namespace Jsonapinator.Document;

/// <summary>
/// The "source" member of a JSON:API error object (https://jsonapi.org/format/#errors).
/// </summary>
public sealed class ErrorSourceObject
{
    public string? Pointer { get; set; }

    public string? Parameter { get; set; }
}
