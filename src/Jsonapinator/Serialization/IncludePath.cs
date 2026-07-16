namespace Jsonapinator.Serialization;

/// <summary>
/// One node in the merged tree of dot-notation include paths (e.g. "author", "comments.author").
/// Merging into a tree (rather than walking each path string independently) ensures a shared
/// prefix like "comments" is only walked once even if it appears in multiple requested paths.
/// </summary>
internal sealed class IncludeNode
{
    public Dictionary<string, IncludeNode> Children { get; } = new();
}

internal static class IncludeTreeBuilder
{
    public static IncludeNode Build(IEnumerable<string> paths)
    {
        var root = new IncludeNode();

        foreach (var path in paths)
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                throw new Exceptions.JsonApiMappingException($"Include path '{path}' is empty or invalid.");
            }

            var node = root;
            foreach (var segment in segments)
            {
                if (!node.Children.TryGetValue(segment, out var child))
                {
                    child = new IncludeNode();
                    node.Children[segment] = child;
                }

                node = child;
            }
        }

        return root;
    }
}
