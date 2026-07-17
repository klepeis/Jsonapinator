using System.Collections;
using System.Globalization;
using System.Text.Json;
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
    private readonly JsonApiSerializerOptions _options;

    public ResourceGraphBuilder(IResourceTypeResolver resolver, JsonApiSerializerOptions? options = null)
    {
        _resolver = resolver;
        _options = options ?? new JsonApiSerializerOptions();
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
            Type = ResolveTypeName(metadata, resource),
            Id = FormatId(metadata.IdProperty.GetValue(resource)),
        };

        if (metadata.Attributes.Count > 0)
        {
            resourceObject.Attributes = metadata.Attributes.ToDictionary(
                a => a.JsonName,
                a => BuildAttributeValue(resource, a));
        }

        if (metadata.Relationships.Count > 0)
        {
            resourceObject.Relationships = metadata.Relationships.ToDictionary(
                r => r.Name,
                r => BuildRelationship(resource, r));
        }

        if (metadata.MetaProperty is not null)
        {
            resourceObject.Meta = (MetaObject?)metadata.MetaProperty.GetValue(resource);
        }

        if (metadata.LinksProperty is not null)
        {
            resourceObject.Links = (LinksObject?)metadata.LinksProperty.GetValue(resource);
        }

        return resourceObject;
    }

    /// <summary>
    /// For a polymorphic attribute (its declared type carries
    /// <see cref="System.Text.Json.Serialization.JsonPolymorphicAttribute"/>), pre-serializes via
    /// the property's DECLARED type rather than leaving the raw runtime value for
    /// <see cref="JsonApiDocumentWriter"/> to serialize later — System.Text.Json only embeds a
    /// type discriminator when serialization is driven by the polymorphic base type; serializing
    /// via the concrete runtime type (as the writer otherwise would, using <c>value.GetType()</c>)
    /// bypasses the base type's polymorphic contract and silently drops the discriminator.
    /// </summary>
    private static object? BuildAttributeValue(object resource, AttributeMetadata attribute)
    {
        var rawValue = attribute.Property.GetValue(resource);

        return attribute.IsPolymorphic && rawValue is not null
            ? JsonSerializer.SerializeToNode(rawValue, attribute.Property.PropertyType, NestedValueSerialization.CamelCase)
            : rawValue;
    }

    private RelationshipObject BuildRelationship(object resource, RelationshipMetadata relationshipMetadata)
    {
        var value = relationshipMetadata.Property.GetValue(resource);

        RelationshipObject relationshipObject;
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

            relationshipObject = new RelationshipObject { IsToMany = true, ManyData = identifiers };
        }
        else
        {
            relationshipObject = new RelationshipObject
            {
                IsToMany = false,
                SingleData = value is null ? null : BuildIdentifier(value),
            };
        }

        if (relationshipMetadata.MetaProperty is not null)
        {
            relationshipObject.Meta = (MetaObject?)relationshipMetadata.MetaProperty.GetValue(resource);
        }

        if (relationshipMetadata.LinksProperty is not null)
        {
            relationshipObject.Links = (LinksObject?)relationshipMetadata.LinksProperty.GetValue(resource);
        }

        return relationshipObject;
    }

    private ResourceIdentifierObject BuildIdentifier(object relatedResource)
    {
        var metadata = _resolver.Resolve(relatedResource.GetType());
        return new ResourceIdentifierObject
        {
            Type = ResolveTypeName(metadata, relatedResource),
            Id = FormatId(metadata.IdProperty.GetValue(relatedResource)),
        };
    }

    private static string ResolveTypeName(ResourceMetadata metadata, object instance)
    {
        if (metadata.TypeProperty is not null &&
            metadata.TypeProperty.GetValue(instance) is string { Length: > 0 } value)
        {
            return value;
        }

        return metadata.ResourceType;
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
            visited.Add((ResolveTypeName(metadata, root), FormatId(metadata.IdProperty.GetValue(root))));
        }

        var included = new List<ResourceObject>();
        foreach (var root in roots)
        {
            WalkIncludes(root, tree, visited, included, depth: 0);
        }

        if (included.Count > 0)
        {
            document.Included = included;
        }
    }

    private void WalkIncludes(
        object resource, IncludeNode node, HashSet<(string Type, string Id)> visited, List<ResourceObject> included, int depth)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        if (depth > _options.MaxIncludeDepth)
        {
            throw new Exceptions.JsonApiMappingException(
                $"Include-path walk exceeded the configured maximum depth of {_options.MaxIncludeDepth} " +
                $"({nameof(JsonApiSerializerOptions.MaxIncludeDepth)}).");
        }

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
                var key = (ResolveTypeName(relatedMetadata, related), FormatId(relatedMetadata.IdProperty.GetValue(related)));

                if (visited.Add(key))
                {
                    if (included.Count >= _options.MaxIncludedResources)
                    {
                        throw new Exceptions.JsonApiMappingException(
                            $"The 'included' array would contain more than {_options.MaxIncludedResources} resources, " +
                            $"exceeding the configured limit ({nameof(JsonApiSerializerOptions.MaxIncludedResources)}).");
                    }

                    included.Add(BuildResource(related));
                }

                WalkIncludes(related, childNode, visited, included, depth + 1);
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
