using Jsonapinator.Document;

namespace Jsonapinator.Serialization;

/// <summary>
/// Converts a spec-shaped <see cref="JsonApiDocument"/> to its JSON representation.
/// </summary>
public interface IJsonApiWriter
{
    string Write(JsonApiDocument document);
}
