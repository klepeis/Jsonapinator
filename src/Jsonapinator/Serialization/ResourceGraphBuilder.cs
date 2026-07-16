using System.Collections;
using System.Globalization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Metadata;

namespace Jsonapinator.Serialization;

/// <summary>
/// Converts POCOs mapped via <c>Jsonapinator.Attributes</c> into the spec-shaped
/// <see cref="Document"/> model, using <see cref="IResourceTypeResolver"/> for mapping metadata.
/// </summary>
public sealed class ResourceGraphBuilder
{
    private readonly IResourceTypeResolver _resolver;

    public ResourceGraphBuilder(IResourceTypeResolver resolver)
    {
        _resolver = resolver;
    }

    public JsonApiDocument BuildDocument(object resource) =>
        JsonApiDocument.ForSingleResource(BuildResource(resource));

    public JsonApiDocument BuildDocument(object resource, IEnumerable<string>? includePaths)
    {
        var document = BuildDocument(resource);
        ApplyIncludes(document, new[] { resource }, includePaths);
        return document;
    }

    public JsonApiDocument BuildCollectionDocument(IEnumerable resources)
    {
        var resourceObjects = new List<ResourceObject>();
        foreach (var resource in resources)
        {
            resourceObjects.Add(BuildResource(resource));
        }

        return JsonApiDocument.ForCollection(resourceObjects);
    }

    public JsonApiDocument BuildCollectionDocument(IEnumerable resources, IEnumerable<string>? includePaths)
    {
        var rootResources = new List<object>();
        var resourceObjects = new List<ResourceObject>();
        foreach (var resource in resources)
        {
            rootResources.Add(resource);
            resourceObjects.Add(BuildResource(resource));
        }

        var document = JsonApiDocument.ForCollection(resourceObjects);
        ApplyIncludes(document, rootResources, includePaths);
        return document;
    }

    public ResourceObject BuildResource(object resource)
    {
        var metadata = _resolver.Resolve(resource.GetType());

        var resourceObject = new ResourceObject
        {
            Type = metadata.ResourceType,
            Id = FormatId(metadata.IdProperty.GetValue(resource)),
        };

        if (metadata.Attributes.Count > 0)
        {
            resourceObject.Attributes = metadata.Attributes.ToDictionary(
                a => a.JsonName,
                a => a.Property.GetValue(resource));
        }

        if (metadata.Relationships.Count > 0)
        {
            resourceObject.Relationships = metadata.Relationships.ToDictionary(
                r => r.Name,
                r => BuildRelationship(resource, r));
        }

        return resourceObject;
    }

    private RelationshipObject BuildRelationship(object resource, RelationshipMetadata relationshipMetadata)
    {
        var value = relationshipMetadata.Property.GetValue(resource);

        if (relationshipMetadata.Kind == RelationshipKind.ToMany)
        {
            var identifiers = new List<ResourceIdentifierObject>();
            if (value is IEnumerable enumerable)
            {
                foreach (var element in enumerable)
                {
                    identifiers.Add(BuildIdentifier(element));
                }
            }

            return new RelationshipObject { IsToMany = true, ManyData = identifiers };
        }

        return new RelationshipObject
        {
            IsToMany = false,
            SingleData = value is null ? null : BuildIdentifier(value),
        };
    }

    private ResourceIdentifierObject BuildIdentifier(object relatedResource)
    {
        var metadata = _resolver.Resolve(relatedResource.GetType());
        return new ResourceIdentifierObject
        {
            Type = metadata.ResourceType,
            Id = FormatId(metadata.IdProperty.GetValue(relatedResource)),
        };
    }

    private void ApplyIncludes(JsonApiDocument document, IEnumerable<object> rootResources, IEnumerable<string>? includePaths)
    {
        if (includePaths is null)
        {
            return;
        }

        var paths = includePaths.ToList();
        if (paths.Count == 0)
        {
            return;
        }

        var tree = IncludeTreeBuilder.Build(paths);
        var roots = rootResources.ToList();

        var visited = new HashSet<(string Type, string Id)>();
        foreach (var root in roots)
        {
            var metadata = _resolver.Resolve(root.GetType());
            visited.Add((metadata.ResourceType, FormatId(metadata.IdProperty.GetValue(root))));
        }

        var included = new List<ResourceObject>();
        foreach (var root in roots)
        {
            WalkIncludes(root, tree, visited, included);
        }

        if (included.Count > 0)
        {
            document.Included = included;
        }
    }

    private void WalkIncludes(object resource, IncludeNode node, HashSet<(string Type, string Id)> visited, List<ResourceObject> included)
    {
        var metadata = _resolver.Resolve(resource.GetType());

        foreach (var (segmentName, childNode) in node.Children)
        {
            var relationshipMetadata = metadata.Relationships.FirstOrDefault(r => r.Name == segmentName)
                ?? throw new Exceptions.JsonApiMappingException(
                    $"Include path segment '{segmentName}' is not a relationship on resource type '{metadata.ResourceType}'.");

            var rawValue = relationshipMetadata.Property.GetValue(resource);
            var relatedObjects = relationshipMetadata.Kind == RelationshipKind.ToMany
                ? (rawValue as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>()
                : rawValue is null ? Enumerable.Empty<object>() : new[] { rawValue };

            foreach (var related in relatedObjects)
            {
                var relatedMetadata = _resolver.Resolve(related.GetType());
                var key = (relatedMetadata.ResourceType, FormatId(relatedMetadata.IdProperty.GetValue(related)));

                if (visited.Add(key))
                {
                    included.Add(BuildResource(related));
                }

                WalkIncludes(related, childNode, visited, included);
            }
        }
    }

    private static string FormatId(object? idValue) => idValue switch
    {
        null => throw new Exceptions.JsonApiMappingException("A resource's [JsonApiId] property must not be null."),
        string s => s,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => idValue.ToString()!,
    };
}
