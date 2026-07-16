namespace Jsonapinator.Document;

/// <summary>
/// A JSON:API "links object" (https://jsonapi.org/format/#document-links). V1 supports
/// simple string-valued links only (no href/meta link objects).
/// </summary>
public sealed class LinksObject : Dictionary<string, string?>
{
}
