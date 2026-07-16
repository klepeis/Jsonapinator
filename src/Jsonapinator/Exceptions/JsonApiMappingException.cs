namespace Jsonapinator.Exceptions;

/// <summary>
/// Thrown for structural/programmer errors in resource mapping (missing or malformed
/// <c>Jsonapinator.Attributes</c> declarations, malformed JSON:API JSON, id type mismatches).
/// Not used for business-facing JSON:API errors — those are represented as plain
/// <see cref="Jsonapinator.Document.ErrorObject"/> data.
/// </summary>
public sealed class JsonApiMappingException : Exception
{
    public JsonApiMappingException(string message)
        : base(message)
    {
    }

    public JsonApiMappingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
