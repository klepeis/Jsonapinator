using Jsonapinator.Document;

namespace Jsonapinator.Deserialization;

/// <summary>
/// Parses raw JSON into a spec-shaped <see cref="JsonApiDocument"/>.
/// </summary>
public interface IJsonApiReader
{
    JsonApiDocument Read(string json);
}
